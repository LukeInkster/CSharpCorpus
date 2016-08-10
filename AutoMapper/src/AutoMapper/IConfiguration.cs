namespace AutoMapper
{
    using System;
    using System.Collections.Generic;

    public interface IConfiguration : IProfileConfiguration
    {
        Func<Type, object> ServiceCtor { get; }
        IEnumerable<Profile> Profiles { get; }
    }
}