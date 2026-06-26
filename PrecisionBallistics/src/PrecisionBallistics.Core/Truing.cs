using System;
using System.Collections.Generic;

namespace PrecisionBallistics.Core;

/// <summary>
/// "Truing" routines: adjust a load's muzzle velocity or BC so the predicted
/// drop matches a value actually observed on target at a known range. This is
/// how real DOPE is reconciled with the model at distance.
/// </summary>
public static class Truing
{
    /// <summary>
    /// Solve for the muzzle velocity (fps) that makes the predicted elevation at
    /// <paramref name="rangeM"/> equal the observed dial value.
    /// </summary>
    public static double TrueMuzzleVelocity(Cartridge baseCartridge, Rifle rifle, double zeroRangeM,
                                            ShotConditions cond, double rangeM,
                                            double observed, AngularUnit unit)
    {
        var solver = new BallisticSolver();
        double lo = baseCartridge.MuzzleVelocityFps - 400;
        double hi = baseCartridge.MuzzleVelocityFps + 400;

        Func<double, double> err = mv =>
        {
            var c = baseCartridge.Clone();
            c.MuzzleVelocityFps = mv;
            c.MvTempSensitivityFpsPerC = 0;
            var local = CloneConditionsToSingleRange(cond, rangeM);
            var sol = solver.Solve(c, rifle, new Scope(), zeroRangeM, local);
            var p = sol.Points[^1];
            double pred = unit == AngularUnit.Mil ? p.ElevationMil : p.ElevationMoa;
            return pred - observed;
        };

        return Bisect(err, lo, hi);
    }

    /// <summary>
    /// Solve for the ballistic coefficient that makes the predicted elevation at
    /// <paramref name="rangeM"/> equal the observed dial value (MV held fixed).
    /// </summary>
    public static double TrueBallisticCoefficient(Cartridge baseCartridge, Rifle rifle, double zeroRangeM,
                                                  ShotConditions cond, double rangeM,
                                                  double observed, AngularUnit unit)
    {
        var solver = new BallisticSolver();
        double lo = baseCartridge.BallisticCoefficient * 0.6;
        double hi = baseCartridge.BallisticCoefficient * 1.5;

        Func<double, double> err = bc =>
        {
            var c = baseCartridge.Clone();
            c.BallisticCoefficient = bc;
            var local = CloneConditionsToSingleRange(cond, rangeM);
            var sol = solver.Solve(c, rifle, new Scope(), zeroRangeM, local);
            var p = sol.Points[^1];
            double pred = unit == AngularUnit.Mil ? p.ElevationMil : p.ElevationMoa;
            // Higher BC -> less drop -> smaller elevation. Invert sign for monotonic bisection.
            return -(pred - observed);
        };

        return Bisect(err, lo, hi);
    }

    /// <summary>
    /// Solve for the muzzle velocity (fps) that makes the predicted drop below the
    /// line of sight at <paramref name="rangeM"/> equal the observed group offset.
    /// This is field truing: zero at one distance, fire at another, measure how far
    /// the group landed below the point of aim, and back-calculate the velocity.
    /// </summary>
    /// <param name="observedDropM">Observed drop below the point of aim, in metres (always positive).</param>
    public static double TrueMuzzleVelocityFromDrop(Cartridge baseCartridge, Rifle rifle, double zeroRangeM,
                                                    ShotConditions cond, double rangeM, double observedDropM)
    {
        var solver = new BallisticSolver();

        Func<double, double> err = mv =>
        {
            var c = baseCartridge.Clone();
            c.MuzzleVelocityFps = mv;
            c.MvTempSensitivityFpsPerC = 0;
            var local = new ShotConditions
            {
                Atmosphere = cond.Atmosphere,
                WindSpeedMps = 0,
                WindFromDeg = 0,
                MaxRangeM = rangeM,
                RangeStepM = rangeM,
                LookAngleDeg = 0,
                ApplyCoriolis = false,
                ApplySpinDrift = false,
                ApplyAeroJump = false
            };
            var sol = solver.Solve(c, rifle, new Scope(), zeroRangeM, local);
            var p = sol.Points[^1];
            double predictedDropMag = -p.DropM;      // DropM is negative below LOS
            return predictedDropMag - observedDropM; // decreasing in MV
        };

        return Bisect(err, 300, 5000);
    }

    /// <summary>
    /// Best-fit muzzle velocity (fps) across several observed drops at different
    /// ranges (least-squares). Each observation is (rangeMetres, dropBelowAimMetres).
    /// </summary>
    public static double TrueMuzzleVelocityMultiPoint(Cartridge baseCartridge, Rifle rifle, double zeroRangeM,
                                                      ShotConditions cond, IReadOnlyList<(double rangeM, double dropM)> obs)
    {
        var solver = new BallisticSolver();

        Func<double, double> sse = mv =>
        {
            var c = baseCartridge.Clone();
            c.MuzzleVelocityFps = mv;
            c.MvTempSensitivityFpsPerC = 0;
            double s = 0;
            foreach (var o in obs)
            {
                var local = new ShotConditions
                {
                    Atmosphere = cond.Atmosphere,
                    WindSpeedMps = 0, WindFromDeg = 0,
                    MaxRangeM = o.rangeM, RangeStepM = o.rangeM,
                    ApplyCoriolis = false, ApplySpinDrift = false, ApplyAeroJump = false
                };
                var sol = solver.Solve(c, rifle, new Scope(), zeroRangeM, local);
                double pred = -sol.Points[^1].DropM;
                double e = pred - o.dropM;
                s += e * e;
            }
            return s;
        };

        return GoldenMin(sse, 300, 5000, 70);
    }

    private static double GoldenMin(Func<double, double> f, double a, double b, int iters)
    {
        double gr = (Math.Sqrt(5.0) - 1.0) / 2.0;
        double c = b - gr * (b - a), d = a + gr * (b - a);
        double fc = f(c), fd = f(d);
        for (int i = 0; i < iters; i++)
        {
            if (fc < fd) { b = d; d = c; fd = fc; c = b - gr * (b - a); fc = f(c); }
            else { a = c; c = d; fc = fd; d = a + gr * (b - a); fd = f(d); }
        }
        return 0.5 * (a + b);
    }

    private static ShotConditions CloneConditionsToSingleRange(ShotConditions cond, double rangeM)
    {
        return new ShotConditions
        {
            Atmosphere = cond.Atmosphere,
            WindSpeedMps = 0,
            WindFromDeg = cond.WindFromDeg,
            MaxRangeM = rangeM,
            RangeStepM = rangeM,
            LookAngleDeg = 0,
            ApplyCoriolis = false,
            ApplySpinDrift = false,
            ApplyAeroJump = false
        };
    }

    private static double Bisect(Func<double, double> f, double lo, double hi)
    {
        double flo = f(lo), fhi = f(hi);
        if (flo == 0) return lo;
        if (fhi == 0) return hi;
        if (Math.Sign(flo) == Math.Sign(fhi))
            return Math.Abs(flo) < Math.Abs(fhi) ? lo : hi; // no bracket; return closer end
        for (int i = 0; i < 100; i++)
        {
            double mid = 0.5 * (lo + hi);
            double fm = f(mid);
            if (Math.Abs(fm) < 1e-6) return mid;
            if (Math.Sign(fm) == Math.Sign(flo)) { lo = mid; flo = fm; }
            else { hi = mid; }
        }
        return 0.5 * (lo + hi);
    }
}
