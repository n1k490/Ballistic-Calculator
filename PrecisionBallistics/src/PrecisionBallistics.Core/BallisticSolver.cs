using System;

namespace PrecisionBallistics.Core;

/// <summary>
/// 3-DOF point-mass trajectory solver. Integrates the equations of motion with
/// fourth-order Runge–Kutta using the standard drag model scaled by the
/// projectile's ballistic coefficient, then layers on the empirical long-range
/// corrections (spin drift, aerodynamic jump, Coriolis).
///
/// Coordinate frame (right-handed):
///   x = downrange, y = up, z = shooter's right.
/// </summary>
public sealed class BallisticSolver
{
    private const double Dt = 0.0005;     // integration step, seconds
    private const double MaxFlightTime = 30.0;

    /// <summary>Effective muzzle velocity after applying powder-temperature sensitivity.</summary>
    public static double EffectiveMv(Cartridge c, Atmosphere atm)
    {
        if (c.MvTempSensitivityFpsPerC == 0.0) return c.MuzzleVelocityFps;
        return c.MuzzleVelocityFps + c.MvTempSensitivityFpsPerC * (atm.TemperatureC - c.MvReferenceTempC);
    }

    /// <summary>
    /// Find the launch angle (radians, above the bore-to-LOS line) that places the
    /// trajectory back on the line of sight at <paramref name="zeroRangeM"/>.
    /// </summary>
    public double SolveZeroAngle(Cartridge c, Rifle r, double zeroRangeM, Atmosphere atm)
    {
        double mv = Units.FpsToMps(EffectiveMv(c, atm));
        double bcSi = c.BallisticCoefficient * Units.BcLbIn2ToKgM2;
        double rho = atm.DensityKgM3();
        double aSnd = atm.SpeedOfSoundMps();
        double sh = r.SightHeightIn * Units.Inch;
        var cd = BuildCd(c);

        double lo = -0.02, hi = 0.06; // radians
        for (int iter = 0; iter < 80; iter++)
        {
            double mid = 0.5 * (lo + hi);
            double drop = DropAtRange(mv, mid, bcSi, cd, rho, aSnd, sh, zeroRangeM);
            if (drop > 0) hi = mid; else lo = mid;
        }
        return 0.5 * (lo + hi);
    }

    /// <summary>Build the Mach→Cd function for a cartridge (custom curve, or standard G1/G7).</summary>
    internal static System.Func<double, double> BuildCd(Cartridge c)
    {
        if (c.DragModel == DragModel.Custom && c.HasCustomCurve)
        {
            double[] m = c.CustomMach!, cc = c.CustomCd!;
            return mach => Interp(m, cc, mach);
        }
        DragModel model = c.DragModel == DragModel.Custom ? DragModel.G7 : c.DragModel;
        return mach => DragTables.Cd(model, mach);
    }

    private static double Interp(double[] xs, double[] ys, double x)
    {
        if (x <= xs[0]) return ys[0];
        if (x >= xs[^1]) return ys[^1];
        for (int i = 1; i < xs.Length; i++)
            if (x < xs[i])
            {
                double x0 = xs[i - 1], y0 = ys[i - 1], x1 = xs[i], y1 = ys[i];
                return y0 + (y1 - y0) * (x - x0) / (x1 - x0);
            }
        return ys[^1];
    }

    // Integrate (no wind) and return signed vertical position relative to LOS at the given range.
    private static double DropAtRange(double mv, double angle, double bcSi, System.Func<double, double> cd,
                                      double rho, double aSnd, double sightHeight, double rangeM)
    {
        double x = 0, y = -sightHeight, vx = mv * Math.Cos(angle), vy = mv * Math.Sin(angle);
        double t = 0;
        while (x < rangeM && t < MaxFlightTime)
        {
            double px = x, py = y;
            // RK4 in (x,y,vx,vy)
            var (kx1, ky1, kvx1, kvy1) = D2(x, y, vx, vy, bcSi, cd, rho, aSnd);
            var (kx2, ky2, kvx2, kvy2) = D2(x + 0.5 * Dt * kx1, y + 0.5 * Dt * ky1, vx + 0.5 * Dt * kvx1, vy + 0.5 * Dt * kvy1, bcSi, cd, rho, aSnd);
            var (kx3, ky3, kvx3, kvy3) = D2(x + 0.5 * Dt * kx2, y + 0.5 * Dt * ky2, vx + 0.5 * Dt * kvx2, vy + 0.5 * Dt * kvy2, bcSi, cd, rho, aSnd);
            var (kx4, ky4, kvx4, kvy4) = D2(x + Dt * kx3, y + Dt * ky3, vx + Dt * kvx3, vy + Dt * kvy3, bcSi, cd, rho, aSnd);
            x += Dt / 6 * (kx1 + 2 * kx2 + 2 * kx3 + kx4);
            y += Dt / 6 * (ky1 + 2 * ky2 + 2 * ky3 + ky4);
            vx += Dt / 6 * (kvx1 + 2 * kvx2 + 2 * kvx3 + kvx4);
            vy += Dt / 6 * (kvy1 + 2 * kvy2 + 2 * kvy3 + kvy4);
            t += Dt;
            if (x >= rangeM)
            {
                double f = (rangeM - px) / (x - px);
                return py + f * (y - py);
            }
        }
        return y;
    }

    private static (double, double, double, double) D2(double x, double y, double vx, double vy,
                                                        double bcSi, System.Func<double, double> cd, double rho, double aSnd)
    {
        double v = Math.Sqrt(vx * vx + vy * vy);
        double mach = v / aSnd;
        double cdv = cd(mach);
        double k = Math.PI * rho * cdv / (8.0 * bcSi);
        double ax = -k * v * vx;
        double ay = -k * v * vy - Units.Gravity;
        return (vx, vy, ax, ay);
    }

    /// <summary>Compute a full trajectory solution for the given profile and conditions.</summary>
    public TrajectorySolution Solve(Cartridge c, Rifle r, Scope scope, double zeroRangeM, ShotConditions cond)
    {
        var atm = cond.Atmosphere;
        double mvFps = EffectiveMv(c, atm);
        double mv = Units.FpsToMps(mvFps);
        double bcSi = c.BallisticCoefficient * Units.BcLbIn2ToKgM2;
        double rho = atm.DensityKgM3();
        double aSnd = atm.SpeedOfSoundMps();
        double sh = r.SightHeightIn * Units.Inch;
        double massKg = c.WeightGr * Units.Grain;

        double zeroAngle = SolveZeroAngle(c, r, zeroRangeM, atm);
        double sg = Stability.MillerSg(c, r, mvFps, atm);

        // Wind decomposition (meteorological "from" convention).
        double wr = cond.WindFromDeg * Math.PI / 180.0;
        double windX = -cond.WindSpeedMps * Math.Cos(wr); // +x downrange; headwind -> negative
        double windZ = -cond.WindSpeedMps * Math.Sin(wr); // +z right

        // Look-angle: gravity acts along true vertical; we tilt the LOS. Use rifleman's
        // correction by scaling the gravity component along the sight plane.
        double look = cond.LookAngleDeg * Math.PI / 180.0;
        var cdOf = BuildCd(c);

        var sol = new TrajectorySolution
        {
            ZeroAngleRad = zeroAngle,
            ZeroAngleMoa = zeroAngle * Units.MoaPerRadian,
            AirDensityKgM3 = rho,
            SpeedOfSoundMps = aSnd,
            DensityAltitudeFt = atm.DensityAltitudeFt(),
            StabilityFactor = sg,
            EffectiveMuzzleVelocityFps = mvFps
        };

        // State: x downrange, y vertical (LOS-relative handled at output), z horizontal.
        double x = 0, y = -sh, z = 0;
        double vx = mv * Math.Cos(zeroAngle + look);
        double vy = mv * Math.Sin(zeroAngle + look);
        double vz = 0;
        double t = 0;

        double nextRange = cond.RangeStepM;
        double transonic = 0;
        double prevMach = mv / aSnd;

        (double, double, double, double, double, double) Deriv(double X, double Y, double Z,
                                                               double VX, double VY, double VZ)
        {
            double rvx = VX - windX, rvy = VY, rvz = VZ - windZ;
            double vrel = Math.Sqrt(rvx * rvx + rvy * rvy + rvz * rvz);
            double mach = vrel / aSnd;
            double cd = cdOf(mach);
            double k = Math.PI * rho * cd / (8.0 * bcSi);
            double ax = -k * vrel * rvx;
            double ay = -k * vrel * rvy - Units.Gravity * Math.Cos(look);
            double az = -k * vrel * rvz;
            return (VX, VY, VZ, ax, ay, az);
        }

        while (x < cond.MaxRangeM + 1e-6 && t < MaxFlightTime)
        {
            double px = x, py = y, pz = z, pt = t;
            double pvx = vx, pvy = vy, pvz = vz;

            var (kx1, ky1, kz1, kvx1, kvy1, kvz1) = Deriv(x, y, z, vx, vy, vz);
            var (kx2, ky2, kz2, kvx2, kvy2, kvz2) = Deriv(x + 0.5 * Dt * kx1, y + 0.5 * Dt * ky1, z + 0.5 * Dt * kz1, vx + 0.5 * Dt * kvx1, vy + 0.5 * Dt * kvy1, vz + 0.5 * Dt * kvz1);
            var (kx3, ky3, kz3, kvx3, kvy3, kvz3) = Deriv(x + 0.5 * Dt * kx2, y + 0.5 * Dt * ky2, z + 0.5 * Dt * kz2, vx + 0.5 * Dt * kvx2, vy + 0.5 * Dt * kvy2, vz + 0.5 * Dt * kvz2);
            var (kx4, ky4, kz4, kvx4, kvy4, kvz4) = Deriv(x + Dt * kx3, y + Dt * ky3, z + Dt * kz3, vx + Dt * kvx3, vy + Dt * kvy3, vz + Dt * kvz3);

            x += Dt / 6 * (kx1 + 2 * kx2 + 2 * kx3 + kx4);
            y += Dt / 6 * (ky1 + 2 * ky2 + 2 * ky3 + ky4);
            z += Dt / 6 * (kz1 + 2 * kz2 + 2 * kz3 + kz4);
            vx += Dt / 6 * (kvx1 + 2 * kvx2 + 2 * kvx3 + kvx4);
            vy += Dt / 6 * (kvy1 + 2 * kvy2 + 2 * kvy3 + kvy4);
            vz += Dt / 6 * (kvz1 + 2 * kvz2 + 2 * kvz3 + kvz4);
            t += Dt;

            double vrelNow = Math.Sqrt((vx - windX) * (vx - windX) + vy * vy + (vz - windZ) * (vz - windZ));
            double machNow = vrelNow / aSnd;
            if (transonic == 0 && prevMach >= 1.0 && machNow < 1.0)
                transonic = x;
            prevMach = machNow;

            while (nextRange <= x + 1e-9 && nextRange <= cond.MaxRangeM + 1e-6)
            {
                double f = (x - px) == 0 ? 0 : (nextRange - px) / (x - px);
                double iy = py + f * (y - py);
                double iz = pz + f * (z - pz);
                double ivx = pvx + f * (vx - pvx);
                double ivy = pvy + f * (vy - pvy);
                double ivz = pvz + f * (vz - pvz);
                double it = pt + f * Dt;
                AddPoint(sol, c, r, scope, cond, sg, nextRange, iy, iz, ivx, ivy, ivz, it, aSnd, massKg, look);
                nextRange += cond.RangeStepM;
            }
        }

        return new TransonicWrapper(sol, transonic).Result;
    }

    // Helper to set the init-only TransonicRangeM after the fact.
    private sealed class TransonicWrapper
    {
        public TrajectorySolution Result { get; }
        public TransonicWrapper(TrajectorySolution s, double transonic)
        {
            var ns = new TrajectorySolution
            {
                ZeroAngleRad = s.ZeroAngleRad,
                ZeroAngleMoa = s.ZeroAngleMoa,
                AirDensityKgM3 = s.AirDensityKgM3,
                SpeedOfSoundMps = s.SpeedOfSoundMps,
                DensityAltitudeFt = s.DensityAltitudeFt,
                StabilityFactor = s.StabilityFactor,
                EffectiveMuzzleVelocityFps = s.EffectiveMuzzleVelocityFps,
                TransonicRangeM = transonic
            };
            ns.Points.AddRange(s.Points);
            Result = ns;
        }
    }

    private static void AddPoint(TrajectorySolution sol, Cartridge c, Rifle r, Scope scope,
                                 ShotConditions cond, double sg, double rangeM,
                                 double yMec, double zWind, double vx, double vy, double vz,
                                 double tof, double aSnd, double massKg, double look)
    {
        double v = Math.Sqrt(vx * vx + vy * vy + vz * vz);
        double mach = v / aSnd;
        double energy = 0.5 * massKg * v * v;

        // Drop relative to line of sight. For inclined fire, LOS rises with the
        // look angle; project the vertical position onto the sight plane.
        double losY = rangeM * Math.Tan(look);
        double dropM = (yMec - losY) * Math.Cos(look);

        // --- horizontal corrections ---
        double windDefl = zWind;

        double spin = 0;
        if (cond.ApplySpinDrift && sg > 0)
        {
            double sdInch = 1.25 * (sg + 1.2) * Math.Pow(tof, 1.83);
            spin = sdInch * Units.Inch;
            if (r.TwistDirection == TwistDirection.Left) spin = -spin;
        }

        double aeroJump = 0;
        if (cond.ApplyAeroJump && sg > 0)
        {
            // Litz aerodynamic jump (vertical) from the crosswind component.
            double wr = cond.WindFromDeg * Math.PI / 180.0;
            double crossMps = cond.WindSpeedMps * Math.Sin(wr);   // +z component of wind source
            double crossMph = Units.MpsToMph(crossMps);
            double ajMoa = 0.01 * sg * crossMph; // approx MOA per Litz
            aeroJump = Math.Tan(ajMoa / Units.MoaPerRadian) * rangeM;
        }

        double corH = 0, corV = 0;
        if (cond.ApplyCoriolis)
        {
            double lat = cond.LatitudeDeg * Math.PI / 180.0;
            double az = cond.AzimuthDeg * Math.PI / 180.0;
            // Horizontal (drift) component depends on latitude; vertical (Eotvos) on az & lat.
            corH = Units.OmegaEarth * rangeM * tof * Math.Sin(lat);
            corV = Units.OmegaEarth * rangeM * tof * Math.Cos(lat) * Math.Sin(az);
        }

        double totalDrop = dropM + corV;
        double totalWind = windDefl + spin + corH + aeroJump * 0; // aeroJump is vertical; fold below
        double totalDropWithJump = totalDrop + aeroJump;

        double elevMil = -Units.Mil(totalDropWithJump, rangeM);
        double elevMoa = -Units.Moa(totalDropWithJump, rangeM);
        double windMil = Units.Mil(totalWind, rangeM);
        double windMoa = Units.Moa(totalWind, rangeM);

        sol.Points.Add(new TrajectoryPoint
        {
            RangeM = rangeM,
            DropM = totalDropWithJump,
            WindageM = totalWind,
            WindDeflectionM = windDefl,
            SpinDriftM = spin,
            CoriolisHorizM = corH,
            CoriolisVertM = corV,
            AeroJumpM = aeroJump,
            ElevationMil = elevMil,
            ElevationMoa = elevMoa,
            WindageMil = windMil,
            WindageMoa = windMoa,
            VelocityMps = v,
            Mach = mach,
            EnergyJ = energy,
            TimeOfFlightS = tof
        });
    }
}
