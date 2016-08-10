﻿using System.ComponentModel;

namespace StackExchange.Opserver.Data
{
    public enum MonitorStatus
    {
        [Description("good")]
        Good = 0,
        [Description("unknown")]
        Unknown = 1,
        [Description("maintenance")]
        Maintenance = 2,
        [Description("warning")]
        Warning = 3,
        [Description("critical")]
        Critical = 4
    }

    public interface IMonitedService : IMonitorStatus { }

    public interface IMonitorStatus
    {
        MonitorStatus MonitorStatus { get; }
        string MonitorStatusReason { get; }
    }
}