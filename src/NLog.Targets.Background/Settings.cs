namespace NLog.Targets.Background;

static class Settings
{
  const string Prefix = "NLogTargetsBackground_";

  public static int ShutdownTimeoutMs = ReadIntFromEnv(nameof(ShutdownTimeoutMs), 10_000);
  public static int WakeupMs = ReadIntFromEnv(nameof(WakeupMs), 25);

  static int ReadIntFromEnv(string key, int defaultValue)
  {
    try
    {
      var str = Environment.GetEnvironmentVariable(Prefix + key)?.Trim();
      if (string.IsNullOrWhiteSpace(str)) return defaultValue;
      return int.TryParse(str, out var val) ? val : defaultValue;
    }
    catch
    {
      return defaultValue;
    }
  }
}