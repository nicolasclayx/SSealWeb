// SSealEngine/Engine.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace SSealEngine
{
    public enum MotionType
    {
        Static,
        Dynamic,
        Both
    }

    public class SealSize
    {
        public string PartNumber { get; set; } = "";
        public double ID_mm { get; set; }
        public double CS_mm { get; set; }            // cross section
        public double OD_mm { get; set; }
        public double MaxPressure_bar { get; set; }  // rated pressure at reference temp
        public double MaxTemp_C { get; set; } = 150; // recommended max temperature
        public List<string> CompatibleMaterials { get; set; } = new();
        public MotionType MotionCompatibility { get; set; } = MotionType.Both;
        public double MaxSpeed_m_per_s { get; set; } = double.PositiveInfinity; // for dynamic seals
        public string Notes { get; set; } = "";
    }

    public class SSealEngine
    {
        public List<SealSize> Catalog { get; } = new();

        public SSealEngine()
        {
            // Example seed catalog - expand or load externally
            Catalog.AddRange(new[]
            {
                new SealSize { PartNumber="SS-6210-40V", ID_mm=95.2, CS_mm=4.0, OD_mm=103.2, MaxPressure_bar=200, MaxTemp_C=150, CompatibleMaterials = new(){"NBR","FKM"}, MotionCompatibility = MotionType.Both },
                new SealSize { PartNumber="SS-6212-50V", ID_mm=100.5, CS_mm=5.0, OD_mm=110.5, MaxPressure_bar=250, MaxTemp_C=160, CompatibleMaterials = new(){"FKM","FFKM"}, MotionCompatibility = MotionType.Dynamic, MaxSpeed_m_per_s=5 },
                new SealSize { PartNumber="SS-6225-50V", ID_mm=125.0, CS_mm=5.0, OD_mm=135.0, MaxPressure_bar=300, MaxTemp_C=200, CompatibleMaterials = new(){"FFKM"}, MotionCompatibility = MotionType.Static },
                new SealSize { PartNumber="SS-6230-60F", ID_mm=180.0, CS_mm=6.0, OD_mm=192.0, MaxPressure_bar=400, MaxTemp_C=220, CompatibleMaterials = new(){"FFKM"}, MotionCompatibility = MotionType.Both }
            });
        }

        // Add seal programmatically
        public void AddSeal(SealSize seal) => Catalog.Add(seal);

        // Material compatibility check
        public bool IsMaterialCompatible(SealSize s, string material)
            => s.CompatibleMaterials?.Any(m => string.Equals(m, material, StringComparison.OrdinalIgnoreCase)) ?? false;

        // Temperature derating example (more refined curves can be implemented)
        // Returns derating factor [0..1]
        public double TemperatureDerateFactor(double tempC)
        {
            if (tempC <= 100) return 1.0;
            if (tempC <= 150) return 0.9;
            if (tempC <= 200) return 0.75;
            return 0.5;
        }

        // Derated allowable pressure (bar)
        public double DeratePressure(SealSize s, double tempC)
        {
            double factor = TemperatureDerateFactor(tempC);
            return s.MaxPressure_bar * factor;
        }

        // Motion compatibility check
        public bool IsMotionCompatible(SealSize s, MotionType motion, double speed_m_per_s = 0)
        {
            if (s.MotionCompatibility == MotionType.Both) return true;
            if (s.MotionCompatibility != motion) return false;
            if (motion == MotionType.Dynamic && speed_m_per_s > 0 && double.IsFinite(s.MaxSpeed_m_per_s))
            {
                return speed_m_per_s <= s.MaxSpeed_m_per_s;
            }
            return true;
        }

        // Core recommendation algorithm using weighted score
        public (SealSize? Best, double Score, string Reason) RecommendSeal(
            double bore_mm,
            double groove_cs_mm,
            int tempC,
            string medium,
            double systemPressure_bar = 0,
            MotionType motion = MotionType.Both,
            double speed_m_per_s = 0,
            IEnumerable<string>? preferredMaterials = null)
        {
            preferredMaterials ??= Array.Empty<string>();
            SealSize? best = null;
            double bestScore = double.MaxValue;
            string bestReason = "";

            foreach (var s in Catalog)
            {
                // filter out if material preference exists and incompatible
                if (preferredMaterials.Any() && !preferredMaterials.Any(pm => IsMaterialCompatible(s, pm)))
                {
                    continue; // not acceptable
                }

                // material compatibility with medium - simplified: if one of s.CompatibleMaterials matches
                bool materialLooksGood = s.CompatibleMaterials.Any(m => medium.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0) 
                                          || s.CompatibleMaterials.Any(); // fallback: allow and penalize below

                // motion check
                if (!IsMotionCompatible(s, motion, speed_m_per_s))
                {
                    continue;
                }

                // compute derated pressure of this seal at requested temp
                var deratedAllow = DeratePressure(s, tempC);

                // pressure penalty if system pressure > derated allowance
                double pressurePenalty = systemPressure_bar > deratedAllow ? 1e6 + (systemPressure_bar - deratedAllow) * 1000 : 0;

                // geometric mismatch penalty (ID and CS)
                double idPenalty = Math.Abs(s.ID_mm - bore_mm);      // mm difference
                double csPenalty = Math.Abs(s.CS_mm - groove_cs_mm); // mm diff

                // temperature suitability penalty
                double tempPenalty = tempC > s.MaxTemp_C ? (tempC - s.MaxTemp_C) * 50 : 0;

                // material compatibility penalty
                double matPenalty = preferredMaterials.Any() ? (IsMaterialCompatible(s, preferredMaterials.First()) ? 0 : 50) : 0;
                // small penalty if no exact material match to medium
                double mediumPenalty = s.CompatibleMaterials.Any(m => medium.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0) ? 0 : 10;

                // overall weighted score
                // tune these weights as needed: id major, cs medium, temp, material, pressure critical
                double score = idPenalty * 10 + csPenalty * 5 + tempPenalty + matPenalty + mediumPenalty + pressurePenalty;

                // prefer lower score, tie-breaker: higher deratedAllow
                if (score < bestScore || (Math.Abs(score - bestScore) < 1e-6 && deratedAllow > (best != null ? DeratePressure(best, tempC) : 0)))
                {
                    bestScore = score;
                    best = s;
                    bestReason = $"score {score:F2} (idΔ={idPenalty}, csΔ={csPenalty}, tempPen={tempPenalty}, matPen={matPenalty}, presPen={pressurePenalty})";
                }
            }

            return (best, bestScore == double.MaxValue ? double.NaN : bestScore, bestReason);
        }

        // Optional: load catalog from JSON (string) - simple example
        public void LoadCatalogFromJson(string json)
        {
            // implement JSON deserialization depending on your preferred library (System.Text.Json)
            // Example left for you to wire in
            throw new NotImplementedException("LoadCatalogFromJson not implemented - wire in System.Text.Json as desired.");
        }
    }
}
