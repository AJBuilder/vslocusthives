using System;
using System.Collections.Generic;
using LocustHives.Game.Core;
using Vintagestory.API.Common;

/// <summary>
/// A handle into accessing the data of a hive.
/// May go stale. However, will only go stale after all members
/// have been detuned and notified.
/// </summary>
public struct HiveHandle
{
    HiveData hiveData;
    Action<IHiveMember, HiveData> doTune;

    public HiveHandle(HiveData hiveData, Action<IHiveMember, HiveData> doTune)
    {
        this.hiveData = hiveData;
        this.doTune = doTune;
    }

    public uint Id => hiveData.id;

    public string Name
    {
        get => hiveData.name;
        set => hiveData.name = value;
    }

    public IReadOnlySet<IHiveMember> Members => hiveData.members;

    /// <summary>
    /// Tunes the given member to this hive.
    /// </summary>
    /// <param name="member"></param>
    /// <returns></returns>
    public void Tune(IHiveMember member) => doTune(member, hiveData);
}