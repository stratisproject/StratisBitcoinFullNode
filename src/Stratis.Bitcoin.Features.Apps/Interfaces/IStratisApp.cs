﻿namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IStratisApp
    {
        string DisplayName { get; }

        string Location { get; }     
        
        string WebRoot { get; }
    }
}
