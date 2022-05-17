using System.Collections;
using System.Linq;

namespace Swordfish.Library.Diagnostics
{
    public static class Profiler
    {
        //  Internal profiler's storage
        private static Queue mainProfile;
        private static Queue ecsProfile;
        private static Queue physicsProfile;

        public static int HistoryLength = 300;

        /// <summary>
        /// Dummy method to force construction of the static class
        /// </summary>
        public static void Initialize() { }

        static Profiler()
        {
            Debug.Log("Profiler initialized");

            mainProfile = new Queue();
            ecsProfile = new Queue();
            physicsProfile = new Queue();
        }

        /// <summary>
        /// Update provided profile with currentTime.
        /// If paused, all info will be collected and output but the profile wont be stepped through.
        /// This should be called every step you are profiling.
        /// Outputs the highest, lowest, and average timings in the profile.
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="currentTime"></param>
        /// <param name="paused">Pause stepping through the profile</param>
        /// <param name="highest"></param>
        /// <param name="lowest"></param>
        /// <param name="average"></param>
        /// <param name="timings"></param>
        public static void Collect(ProfilerType profilerType, float currentTime, bool paused, out float highest, out float lowest, out float average, out float[] timings)
        {
            Queue profile;
            switch (profilerType)
            {
                case ProfilerType.PHYSICS:
                    profile = physicsProfile;
                    break;
                case ProfilerType.ECS:
                    profile = ecsProfile;
                    break;
                case ProfilerType.MAIN:
                    profile = mainProfile;
                    break;
                default:
                    profile = mainProfile;
                    break;
            }

            //  Make certain the profile is within bounds
            if (profile.Count != HistoryLength)
            {
                while (profile.Count < HistoryLength)
                    profile.Enqueue(0f);
                while (profile.Count > HistoryLength)
                    profile.Dequeue();
            }

            //  Step through the profile if collection isn't paused
            if (!paused)
            {
                //  Add current thread timing to the profile
                profile.Enqueue(currentTime * 1000f);

                //  Remove the oldest thread timing
                profile.Dequeue();
            }

            //  Collect the highest, lowest, and average timings in the current profile...
            highest = 0f;
            lowest = 999f;
            average = 0f;

            foreach (float value in profile)
            {
                if (value < lowest && value > 0f) lowest = value;
                if (value > highest) highest = value;
                average += value;
            }

            average /= profile.Count;

            //  Cast the queue to an array that can be fed into the UI
            timings = profile.Cast<float>().ToArray();
        }
    }
}