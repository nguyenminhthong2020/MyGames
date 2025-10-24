using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGames.Desktop.Services
{
    public class AccuracyTracker
    {
        private readonly List<double> _centipawnLossWhite = new();
        private readonly List<double> _centipawnLossBlack = new();

        // --- Bảng tham chiếu tương đương chess.com ---
        // cpLoss (centipawn) -> Accuracy (%)
        private static readonly (double cp, double acc)[] ReferenceTable =
        {
            (0, 100),
            (20, 99.5),
            (50, 98.5),
            (100, 96.5),
            (150, 94.0),
            (200, 90.0),
            (300, 85.0),
            (400, 75.0),
            (600, 65.0),
            (800, 50.0),
            (1000, 40.0),
            (1500, 25.0),
            (2000, 15.0),
            (3000, 5.0),
            (5000, 0)
        };
        // --- Hàm nội suy Accuracy từ bảng ---
        private double CpToAccuracy(double cp)
        {
            if (cp <= 0) return 100;
            if (cp >= ReferenceTable[^1].cp) return 0;

            for (int i = 1; i < ReferenceTable.Length; i++)
            {
                var (x1, y1) = ReferenceTable[i - 1];
                var (x2, y2) = ReferenceTable[i];

                if (cp <= x2)
                {
                    double t = (cp - x1) / (x2 - x1);
                    return Math.Round(y1 + t * (y2 - y1), 1);
                }
            }

            return 0;
        }

        // Thêm điểm centipawn loss sau mỗi nước cờ
        public void AddMove(string side, double centipawnLoss)
        {
            if (side.Equals("white", StringComparison.OrdinalIgnoreCase))
                _centipawnLossWhite.Add(centipawnLoss);
            else
                _centipawnLossBlack.Add(centipawnLoss);
        }

        // Quy đổi centipawn loss -> accuracy %, theo công thức xấp xỉ chess.com
        private double ConvertCpLossToAccuracy(double cpLoss)
        {
            if (cpLoss <= 10) return 100;
            if (cpLoss >= 800) return 0;
            // Đây là hàm giảm mượt tương tự chess.com
            double acc = 103 - 3 * Math.Pow(Math.Log10(cpLoss + 10), 2) * 20;
            return Math.Clamp(acc, 0, 100);
        }

        // Accuracy trung bình của mỗi bên
        public double GetAverageAccuracy(string side)
        {
            var list = side.Equals("white", StringComparison.OrdinalIgnoreCase)
                ? _centipawnLossWhite
                : _centipawnLossBlack;

            if (list.Count == 0) return 100;
            var accList = list.Select(ConvertCpLossToAccuracy);
            return Math.Round(accList.Average(), 1);
        }

        // Hiển thị dạng chuỗi
        public string GetSummary()
        {
            return $"White: {GetAverageAccuracy("white")}% | Black: {GetAverageAccuracy("black")}%";
        }

        public void Reset()
        {
            _centipawnLossWhite.Clear();
            _centipawnLossBlack.Clear();
        }
    }
}
