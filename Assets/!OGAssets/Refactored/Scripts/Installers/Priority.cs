public static class Priority
{
  private const int MULTIPLIER = 10000;
  public const int STEP = 100;

  public const int CRITICAL = 50 * MULTIPLIER;
  public const int CONFIG = 30 * MULTIPLIER;
  public const int HIGHEST = 20 * MULTIPLIER;
  public const int HIGH = 10 * MULTIPLIER;
  public const int NORMAL = 0 * MULTIPLIER;
  public const int LOW = -10 * MULTIPLIER;
  public const int LOWEST = -20 * MULTIPLIER;
}