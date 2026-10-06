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

    [UnityTest, Timeout(1200000)]
    public IEnumerator CleanedCorridorDrivesInBothDirections()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return CleanupDriveBody(0,5); }

    [UnityTest, Timeout(1200000)]
    public IEnumerator CleanedBridgeDrivesInBothDirections()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return CleanupDriveBody(5,6); }

    [UnityTest, Timeout(1200000)]
    public IEnumerator CleanedRidgeFinishAndShortcutsDriveInBothDirections()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return CleanupDriveBody(6,10); }

    private static IEnumerator CleanupDriveBody(int firstPath,int endPath)
    {
        Director.enabled = false; Laser.enabled = false;
        yield return Mount();
        var terrain = UnityEngine.Object.FindAnyObjectByType<TerrainCollider>();
        Directory.CreateDirectory("Logs/BikeRouteCleanup");
        using var log = new StreamWriter("Logs/BikeRouteCleanup/driving-"+firstPath+".txt");
        // Real controls and Rigidbody travel through the marked canyon, dark cuts and
        // their fork. Reposition only between independent forward/backward runs.
        for (int path = firstPath; path < endPath; path++)
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
            float maximumBoundaryExcess=0;
            bool finished = false;
            yield return Until(() => finished, length / 10 + 20, "Drive cleaned path " + path + " direction " + direction, () =>
            {
                var at = ProjectCleanupPath(Bike.Body.position, path);
                finished = direction > 0 ? at.distance >= length - 18 : at.distance <= 18;
                var ahead = Director.Guide.At(path, Mathf.Clamp(at.distance + direction * (9 + Bike.Speed * .3f), 0, length));
                var target=ahead.position;
                // The bridge ramp is a forward launch, taller than a flat-ground jump.
                // Reverse inspection uses its existing rideable shoulder, like a player would.
                float shoulder=0;
                if((path==5 || path==6) && direction<0)
                {
                    float marker=path==5?398.58646f:502.19f;
                    shoulder=Mathf.SmoothStep(0,1,Mathf.InverseLerp(marker-64,marker-24,ahead.distance))
                        *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(marker+72,marker+122,ahead.distance)));
                    target+=ahead.Right*((path==5?6.7f:-6.4f)*shoulder);
                }
                float turn = Vector3.SignedAngle(Vector3.ProjectOnPlane(Bike.transform.forward, Vector3.up),
                    Vector3.ProjectOnPlane(target - Bike.Body.position, Vector3.up), Vector3.up);
                Input.MoveInput(new Vector2(Mathf.Clamp(turn / 30, -1, 1), Bike.Speed < (shoulder>.1f?18:26) ? 1 : 0));
                float lateral=Vector3.Dot(Bike.Body.position-at.position,at.Right);
                maximumOffset = Mathf.Max(maximumOffset, Mathf.Abs(lateral));
                // The guide marks the driving line, not the outer edge of the original
                // wider Terrain shoulders. Measure against the physical bank itself.
                if(terrain.Raycast(new Ray(at.position+Vector3.up*1.2f,at.Right*Mathf.Sign(lateral)),out var bank,30))
                    maximumBoundaryExcess=Mathf.Max(maximumBoundaryExcess,Mathf.Abs(lateral)-bank.distance);
                bool jump=false;
                if(direction<0 && path!=5)
                    foreach(var hint in Director.Guide.jumps)
                        // Reverse approach reaches the ramp's raised rear face before its forward release marker.
                        if(hint.path==path && !(path==6 && hint.releaseDistance>400) && at.distance>hint.releaseDistance+42 && at.distance<hint.releaseDistance+95)
                            jump=true;
                Input.JumpInput(jump);
                if ((at.distance - nextCapture) * direction >= 0)
                {
                    ProceduralUIReview.Capture($"cleanup-{path}-{Mathf.RoundToInt(nextCapture)}-{direction}", 1280, 720);
                    nextCapture += direction * 110;
                }
            });
            Input.MoveInput(Vector2.zero);Input.JumpInput(false);
            log.WriteLine($"path={path} direction={direction} seconds={Time.time-began:F2} maxOffset={maximumOffset:F2} boundaryExcess={maximumBoundaryExcess:F2} complete={finished}"); log.Flush();
            Assert.That(maximumBoundaryExcess, Is.LessThan(1), "Stay inside the visible original Terrain banks");
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
