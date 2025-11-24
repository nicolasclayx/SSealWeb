namespace SSealEngine
{
    public class SSealEngine
    {
        public class SealSize
        {
            public string PartNumber { get; set; } = "";
            public double ID_mm { get; set; }
            public double CS_mm { get; set; }
            public double OD_mm { get; set; }
            public int MaxPressure_bar { get; set; }
        }

        public List<SealSize> Catalog { get; } = new()
        {
            new SealSize { PartNumber = "SS-6210-40V", ID_mm = 95.2, CS_mm = 4.0, OD_mm = 103.2, MaxPressure_bar = 200 },
            new SealSize { PartNumber = "SS-6212-50V", ID_mm = 100.5, CS_mm = 5.0, OD_mm = 110.5, MaxPressure_bar = 250 },
            new SealSize { PartNumber = "SS-6225-50V", ID_mm = 125.0, CS_mm = 5.0, OD_mm = 135.0, MaxPressure_bar = 300 },
            new SealSize { PartNumber = "SS-6230-60F", ID_mm = 180.0, CS_mm = 6.0, OD_mm = 192.0, MaxPressure_bar = 400 }
        };

        public SealSize? RecommendSeal(double bore, double groove, int temp, string medium)
        {
            SealSize? best = null;
            double bestScore = double.MaxValue;
            foreach (var s in Catalog)
            {
                var score = Math.Abs(s.ID_mm - bore) * 10 + Math.Abs(s.CS_mm - groove);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            return best;
        }

        public double CalculateGrooveDiameter(double bore, string sealType)
        {
            return sealType.Contains("Internal") ? bore * 0.952 :
                   sealType.Contains("External") ? bore * 1.048 :
                   bore * 0.975;
        }

        public double GenerateSqueezePercent(SealSize seal, double groove)
        {
            return (seal.CS_mm - groove) / seal.CS_mm * 100;
        }

        public string GetChemicalRating(string medium, string material)
        {
            if (medium.Contains("Oil") && !material.Contains("EPDM")) return "Green - Excellent";
            if (medium.Contains("Water") && material == "EPDM") return "Green - Excellent";
            return "Yellow - Test Recommended";
        }
    }
}
