using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SubTaskData
{
    public string id;
    public string displayText;
    [NonSerialized] public bool completed;
}

[Serializable]
public sealed class MainTaskData
{
    public string id;
    public string displayText;
    public List<SubTaskData> subTasks = new List<SubTaskData>();
    [NonSerialized] public bool completed;
}
