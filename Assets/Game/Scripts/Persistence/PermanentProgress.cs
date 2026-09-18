using System;
using System.Linq;
using UnityEngine;

// Disk-backed progression; the live PlayerSkillState API remains authoritative for gameplay.
public static class PermanentProgress
{
    private static PermanentSave data;
    private static bool failed, dirty;
    public static PermanentSave Data
    {
        get
        {
            if (data != null) return data;
            try
            {
                data = RunSaveService.PermanentStore.Read<PermanentSave>() ?? new PermanentSave();
                if (data.version != 1) throw new InvalidOperationException("Permanent save version is unsupported.");
            }
            catch (Exception error) { failed = true; data = new PermanentSave(); RunSaveService.ReportError(error); }
            return data;
        }
    }
    public static void Update(SkillData skill, int xp)
    {
        if (!Application.isPlaying || skill == null) return;
        var entry = Data.skills.FirstOrDefault(s => s.id == skill.Id);
        if (entry == null) { entry = new SavedSkill { id = skill.Id }; Data.skills.Add(entry); }
        entry.xp = xp; dirty = true;
    }
    public static bool IsUnread(SkillData skill) => skill != null && Data.skills.Any(s => s.id == skill.Id && !s.acknowledged);
    public static void Acknowledge(SkillData skill)
    {
        var entry = Data.skills.FirstOrDefault(s => skill != null && s.id == skill.Id);
        if (entry == null || entry.acknowledged) return;
        entry.acknowledged = true; dirty = true; Flush();
    }
    public static bool Flush()
    {
        if (failed) return false;
        if (!dirty) return true;
        try { RunSaveService.PermanentStore.Write(Data); dirty = false; return true; }
        catch (Exception error) { RunSaveService.ReportError(error); return false; }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { data = null; dirty = false; failed = false; }
}
