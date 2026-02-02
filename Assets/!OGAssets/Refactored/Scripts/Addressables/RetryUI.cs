// RetryUI.cs
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class RetryUI
{
    public static async UniTask RunStepWithRetry(
        string stepName,
        Func<UniTask> step,
        Action<string> report,
        int maxSilentRetries = 0)
    {
        int attempts = 0;

        while (true)
        {
            try
            {
                attempts++;
                report?.Invoke(stepName + (attempts > 1 ? $" (attempt {attempts})" : ""));
                await step();
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Addressables] {stepName} failed: {ex}");

                if (attempts <= maxSilentRetries)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Min(2 * attempts, 10)));
                    continue;
                }

                bool retry = false;
                if (ErrorModalService.IsReady)
                {
                    retry = await ErrorModalService.ShowRetryCancel(
                        $"Addressables Error — {stepName}",
                        ex.Message,
                        ex.ToString());
                }

                if (!retry) throw;
            }
        }
    }
}