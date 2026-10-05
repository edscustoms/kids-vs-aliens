using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class BikeRouteChasePlayTests
{
    [UnityTest, Timeout(180000)]
    public IEnumerator CleanedAsphaltJoinsDriveInBothDirections()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return CleanupJoinsBody(); }

    private static IEnumerator CleanupJoinsBody()
    {
        Director.enabled = false; Laser.enabled = false; yield return Mount();
        foreach (int join in new[] { 0, 1 })
        foreach (int direction in new[] { 1, -1 })
        {
            int from = direction > 0 ? join : join + 1;
            int to = direction > 0 ? join + 1 : join;
            var start = Director.Guide.At(from, direction > 0 ? Director.Guide.paths[from].Length - 35 : 35);
            Bike.Body.position = start.position + Vector3.up * .85f;
            Bike.Body.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(start.forward * direction, Vector3.up));
            Bike.Body.linearVelocity = Vector3.zero; Bike.Body.angularVelocity = Vector3.zero;
            Input.MoveInput(Vector2.zero); Physics.SyncTransforms(); yield return Seconds(.4f);
            bool crossed = false;
            yield return Until(() => crossed, 12, "Drive asphalt join " + join + " direction " + direction, () =>
            {
                var fromSample = ProjectCleanupPath(Bike.Body.position, from);
                var toSample = ProjectCleanupPath(Bike.Body.position, to);
                var at = (fromSample.position-Bike.Body.position).sqrMagnitude < (toSample.position-Bike.Body.position).sqrMagnitude ? fromSample : toSample;
                crossed = at.path == to && (direction > 0 ? at.distance > 30 : at.distance < Director.Guide.paths[to].Length - 30);
                var ahead = Director.Guide.Ahead(at, direction * (9 + Bike.Speed * .3f), to);
                float turn = Vector3.SignedAngle(Vector3.ProjectOnPlane(Bike.transform.forward, Vector3.up),
                    Vector3.ProjectOnPlane(ahead.position - Bike.Body.position, Vector3.up), Vector3.up);
                Input.MoveInput(new Vector2(Mathf.Clamp(turn / 30, -1, 1), Bike.Speed < 26 ? 1 : 0));
            });
            ProceduralUIReview.Capture($"cleanup-join-{join}-{direction}", 1280, 720);
        }
        Input.MoveInput(Vector2.zero); Complete();
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator CleanedCorridorDrivesInBothDirections()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return CleanupDriveBody(); }

    private static IEnumerator CleanupDriveBody()
    {
        Director.enabled = false; Laser.enabled = false;
        yield return Mount();
        Directory.CreateDirectory("Logs/BikeRouteCleanup");
        using var log = new StreamWriter("Logs/BikeRouteCleanup/driving.txt");
        // Real controls and Rigidbody travel through the marked canyon, dark cuts and
        // their fork. Reposition only between independent forward/backward runs.
        foreach (int path in new[] { 0, 1, 2, 8 })
        foreach (int direction in new[] { 1, -1 })
        {
            float length = Director.Guide.paths[path].Length;
            float from = direction > 0 ? 12 : length - 12;
            var start = Director.Guide.At(path, from);
            Bike.Body.position = start.position + Vector3.up * .85f;
            Bike.Body.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(start.forward * direction, Vector3.up));
            Bike.Body.linearVelocity = Vector3.zero; Bike.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms(); Input.MoveInput(Vector2.zero);
            yield return Seconds(.4f);
            float began = Time.time, nextCapture = from + direction * 55;
            float maximumOffset = 0;
            bool finished = false;
            yield return Until(() => finished, length / 10 + 20, "Drive cleaned path " + path + " direction " + direction, () =>
            {
                var at = ProjectCleanupPath(Bike.Body.position, path);
                finished = direction > 0 ? at.distance >= length - 18 : at.distance <= 18;
                var ahead = Director.Guide.At(path, Mathf.Clamp(at.distance + direction * (9 + Bike.Speed * .3f), 0, length));
                float turn = Vector3.SignedAngle(Vector3.ProjectOnPlane(Bike.transform.forward, Vector3.up),
                    Vector3.ProjectOnPlane(ahead.position - Bike.Body.position, Vector3.up), Vector3.up);
                Input.MoveInput(new Vector2(Mathf.Clamp(turn / 30, -1, 1), Bike.Speed < 26 ? 1 : 0));
                maximumOffset = Mathf.Max(maximumOffset, Mathf.Abs(Vector3.Dot(Bike.Body.position - at.position, at.Right)));
                if ((at.distance - nextCapture) * direction >= 0)
                {
                    ProceduralUIReview.Capture($"cleanup-{path}-{Mathf.RoundToInt(nextCapture)}-{direction}", 1280, 720);
                    nextCapture += direction * 110;
                }
            });
            Input.MoveInput(Vector2.zero);
            log.WriteLine($"path={path} direction={direction} seconds={Time.time-began:F2} maxOffset={maximumOffset:F2} complete={finished}"); log.Flush();
            Assert.That(maximumOffset, Is.LessThan(8), "Stay in the authored driving corridor");
        }
        Complete();
    }
    private static BikeRouteGuide.Sample ProjectCleanupPath(Vector3 position, int path)
    {
        var data = Director.Guide.paths[path];
        float nearest = float.PositiveInfinity, distance = 0;
        for (int i = 1; i < data.points.Length; i++)
        {
            Vector3 delta = data.points[i] - data.points[i-1];
            float t = Mathf.Clamp01(Vector3.Dot(position-data.points[i-1],delta)/delta.sqrMagnitude);
            float error = (position-data.points[i-1]-delta*t).sqrMagnitude;
            if (error >= nearest) continue;
            nearest=error; distance=Mathf.Lerp(data.distances[i-1],data.distances[i],t);
        }
        return Director.Guide.At(path,distance);
    }

}
