using System;
using System.Runtime.InteropServices;

namespace Templates.Performance
{
	public class ExecutionStopwatch
	{
		[DllImport("kernel32.dll")]
		private static extern long GetThreadTimes(IntPtr threadHandle, out long createionTime,
			 out long exitTime, out long kernelTime, out long userTime);

        [DllImport("kernel32.dll")]
        private static extern long QueryThreadCycleTime(IntPtr threadHandle, out ulong cycleTime);

        [DllImport("kernel32.dll")]
		private static extern IntPtr GetCurrentThread();

        private ulong _mEndTicks;
        private ulong _mStartTicks;

        private long _mEndTimeStamp;
		private long _mStartTimeStamp;

		private bool _mIsRunning;

		public void Start()
		{
            _mIsRunning = true;

            _mStartTimeStamp = GetThreadTimes();
            _mStartTicks = GetThreadCpuTicks();
        }

	    public void Stop()
	    {
            _mEndTicks = GetThreadCpuTicks();
	        _mEndTimeStamp = GetThreadTimes();

            _mIsRunning = false;
        }

	    public void Reset()
		{
			_mStartTimeStamp = 0;
			_mEndTimeStamp = 0;
		}

	    public ulong CpuTicks => _mEndTicks - _mStartTicks;

	    public TimeSpan Elapsed
		{
			get
			{
				long elapsed = _mEndTimeStamp - _mStartTimeStamp;
			    TimeSpan result = TimeSpan.FromTicks(elapsed);
				return result;
			}
		}

		public bool IsRunning => _mIsRunning;

	    private ulong GetThreadCpuTicks()
	    {
	        IntPtr threadHandle = GetCurrentThread();

	        long retcode = QueryThreadCycleTime(threadHandle, out var result);

	        bool success = Convert.ToBoolean(retcode);
	        if (!success)
	            throw new Exception($"failed to get timestamp. error code: {retcode}");

	        return result;
	    }

	    private long GetThreadTimes()
		{
			IntPtr threadHandle = GetCurrentThread();

			long retcode = GetThreadTimes(threadHandle, out var _,
				out var _, out var kernelTime, out var userTime);

			bool success = Convert.ToBoolean(retcode);
			if (!success)
				throw new Exception($"failed to get timestamp. error code: {retcode}");

			long result = kernelTime + userTime;
			return result;
		}
	}
}
