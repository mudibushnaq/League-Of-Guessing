using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public readonly struct DownloadProgress
{
    public readonly long DownloadedBytes;
    public readonly long TotalBytes;
    public readonly float Percent;
    public DownloadProgress(long downloaded, long total, float percent)
    { DownloadedBytes = downloaded; TotalBytes = total; Percent = percent; }
}