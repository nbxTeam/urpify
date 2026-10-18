using System.Diagnostics;

namespace Urpify
{
    /// <summary>
    /// Identifies this Editor process (process id plus its start time, so a reused id never matches). Work saved to disk
    /// by one Editor (a capture job, a running step) is resumed only by the same process across its domain reloads;
    /// another Editor that finds it closes it as interrupted. SessionState was tried first and read back empty on a
    /// Play Mode reload in batch mode (ShaderTutorials, M3), so it is not used for this.
    /// </summary>
    public static class EditorSession
    {
        static string id;

        public static string Id
        {
            get
            {
                if (id == null)
                {
                    using (var p = Process.GetCurrentProcess())
                        id = p.Id + ":" + p.StartTime.ToUniversalTime().Ticks;
                }
                return id;
            }
        }
    }
}
