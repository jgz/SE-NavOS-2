using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Recovers the ship's real velocity when the Flip and Burn mod is active.
    ///
    /// Above its trigger speed the mod stops using physics to move the grid: it clamps
    /// Grid.Physics.LinearVelocity to the world ship speed limit and repositions the grid with
    /// Grid.Teleport() every tick. GetShipVelocities() therefore reports the clamp - typically
    /// 1000 m/s - while the ship may actually be doing tens of km/s.
    ///
    /// Position is still truthful, because the teleport target is a real world matrix. Differencing
    /// it across ticks recovers the true velocity with no cooperation from the mod. (The mod does
    /// expose the real figure through a ModAPI delegate, but that is unreachable from a
    /// Programmable Block.)
    ///
    /// Note the client/server split: the mod writes the FULL virtual velocity into physics on the
    /// client to keep the speedometer honest, but the server - where this script runs in
    /// multiplayer - sees the clamped value. Anything built on this must be tested on the server,
    /// not in single-player.
    /// </summary>
    public static class TrueVelocity
    {
        /// <summary>
        /// Reject a single position jump larger than this. An instance transfer moves the grid
        /// hundreds of km at once; 20 km in one tick is 1.2 million m/s, which no drive does.
        ///
        /// This replaces an acceleration-based test that LATCHED. Flip and Burn repositions the
        /// grid by teleport, and the server delivers those updates unevenly - some ticks move
        /// almost nothing, the next moves a long way. Every one of those samples looked like
        /// impossible acceleration, so every one was rejected, and _velocity stayed frozen at the
        /// last value accepted before the clamp engaged. Observed in flight as trk pinned at
        /// exactly 1000 while the ship covered ground at 3100 m/s.
        /// </summary>
        private const double MaxPlausibleJumpMetres = 20000;

        /// <summary>
        /// Smoothing factor. Bursty position updates need averaging, not rejection.
        ///
        /// Measured burst pattern on the server: dpos alternates 16.7 m (the clamped 1000 m/s) and
        /// ~48 m (catching up), tick on tick off. At 0.15 that left the estimate oscillating about
        /// +-5%, which swung the stopping distance +-10% and chattered the decel trigger. 0.06 is
        /// roughly a 16-tick time constant - a quarter of a second - which damps a two-tick
        /// alternation flat while still reacting fast enough for a burn.
        /// </summary>
        private const double Smoothing = 0.06;

        private static Vector3D _lastPosition;
        private static Vector3D _velocity;
        private static bool _havePosition;
        private static bool _haveVelocity;

        /// <summary>Metres moved on the last accepted sample; diagnostic only.</summary>
        public static double LastDeltaMetres;

        /// <summary>Last good position-differenced velocity. Zero until primed.</summary>
        public static Vector3D Value => _velocity;

        /// <summary>Call once per tick, before any navigation controller runs.</summary>
        public static void Sample(Vector3D position, double deltaSeconds)
        {
            if (deltaSeconds <= 0)
                return;

            if (!_havePosition)
            {
                _lastPosition = position;
                _havePosition = true;
                return;
            }

            Vector3D delta = position - _lastPosition;
            _lastPosition = position;

            // Genuine relocation: drop it, do not fold it into the average.
            if (delta.Length() > MaxPlausibleJumpMetres)
                return;

            LastDeltaMetres = delta.Length();
            Vector3D sample = delta / deltaSeconds;

            // First real sample has nothing to compare against, so take it as-is.
            if (!_haveVelocity)
            {
                _velocity = sample;
                _haveVelocity = true;
                return;
            }

            // Low-pass instead of reject. Uneven delivery averages out over a handful of ticks and
            // cannot latch the estimate the way the old filter did.
            _velocity = _velocity * (1 - Smoothing) + sample * Smoothing;
        }

        public static void Reset()
        {
            _havePosition = false;
            _haveVelocity = false;
            _velocity = Vector3D.Zero;
        }
    }
}
