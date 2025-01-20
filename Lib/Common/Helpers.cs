using ACE.Entity;
using ACE.Entity.Models;
using ACE.Mods.Legend.Lib.Common.Spells;

namespace ACE.Mods.Legend.Lib.Common;

public static class Helpers
{
    public static string FormatTimeRemaining(TimeSpan timeRemaining)
    {
        if (timeRemaining.TotalSeconds < 1)
        {
            return "less than a second";
        }
        else if (timeRemaining.TotalMinutes < 1)
        {
            return $"{timeRemaining.Seconds} second{(timeRemaining.Seconds != 1 ? "s" : "")}";
        }
        else if (timeRemaining.TotalHours < 1)
        {
            return $"{timeRemaining.Minutes} minute{(timeRemaining.Minutes != 1 ? "s" : "")} and {timeRemaining.Seconds} second{(timeRemaining.Seconds != 1 ? "s" : "")}";
        }
        else
        {
            return $"{timeRemaining.Hours} hour{(timeRemaining.Hours != 1 ? "s" : "")}, {timeRemaining.Minutes} minute{(timeRemaining.Minutes != 1 ? "s" : "")}, and {timeRemaining.Seconds} second{(timeRemaining.Seconds != 1 ? "s" : "")}";
        }
    }
  }
