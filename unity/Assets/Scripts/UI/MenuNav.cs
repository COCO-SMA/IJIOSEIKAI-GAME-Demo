using UnityEngine;

namespace KunchengRPG.UI
{
    /// <summary>
    /// One cell of the equipment diagram: a slot plus where it sits on screen.
    /// The body is drawn as a human shape, so navigation cannot be a flat list —
    /// pressing right from 左手 has to reach 躯干, not the next array entry.
    /// </summary>
    public struct SlotCell
    {
        public string slot;
        public int col;
        public int row;

        public SlotCell(string slot, int col, int row)
        {
            this.slot = slot; this.col = col; this.row = row;
        }
    }

    /// <summary>
    /// Grid navigation for the body diagram, kept pure so batchmode can test it
    /// without a scene: directional movement over a sparse, ragged grid.
    /// </summary>
    public static class MenuNav
    {
        /// <summary>
        ///        [大脑]
        /// [左手] [躯干] [右手]
        /// [左腿]        [右腿]
        /// [携带一][携带二][携带三]
        /// </summary>
        public static SlotCell[] BodyLayout()
        {
            return new[]
            {
                new SlotCell("brain", 1, 0),
                new SlotCell("left_hand", 0, 1),
                new SlotCell("torso", 1, 1),
                new SlotCell("right_hand", 2, 1),
                new SlotCell("left_leg", 0, 2),
                new SlotCell("right_leg", 2, 2),
                new SlotCell("carry_1", 0, 3),
                new SlotCell("carry_2", 1, 3),
                new SlotCell("carry_3", 2, 3),
            };
        }

        /// <summary>
        /// Move one step in a direction. Picks the candidate that is strictly ahead
        /// on the travel axis, nearest on that axis, breaking ties by cross-axis
        /// distance — so 左腿 pressing up lands on 左手, and 右腿 pressing up lands
        /// on 右手, even though neither column is fully populated.
        /// Returns the current index unchanged when nothing lies that way.
        /// </summary>
        public static int Step(SlotCell[] cells, int current, int dx, int dy)
        {
            if (cells == null || cells.Length == 0) return 0;
            current = Mathf.Clamp(current, 0, cells.Length - 1);
            if (dx == 0 && dy == 0) return current;

            var from = cells[current];
            int best = current;
            int bestMain = int.MaxValue, bestCross = int.MaxValue;

            for (int i = 0; i < cells.Length; i++)
            {
                if (i == current) continue;
                var c = cells[i];

                // Screen rows grow downward, so "up" is a decreasing row.
                int main = dx != 0 ? (c.col - from.col) * dx : (c.row - from.row) * (dy < 0 ? -1 : 1);
                if (main <= 0) continue;

                int cross = dx != 0 ? Mathf.Abs(c.row - from.row) : Mathf.Abs(c.col - from.col);
                if (main > bestMain || (main == bestMain && cross >= bestCross)) continue;

                best = i; bestMain = main; bestCross = cross;
            }

            return best;
        }

        public static int IndexOfSlot(SlotCell[] cells, string slot)
        {
            if (cells == null) return 0;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].slot == slot) return i;
            return 0;
        }

        /// <summary>Wrapping step through a vertical list.</summary>
        public static int StepList(int count, int current, int delta)
        {
            if (count <= 0) return 0;
            return ((current + delta) % count + count) % count;
        }

        /// <summary>
        /// Wrapping step through a backpack grid laid out row-major. Horizontal
        /// movement wraps within the row; vertical movement wraps within the column
        /// and skips past the ragged last row rather than landing on nothing.
        /// </summary>
        public static int StepGrid(int count, int columns, int current, int dx, int dy)
        {
            if (count <= 0 || columns <= 0) return 0;
            current = Mathf.Clamp(current, 0, count - 1);

            int row = current / columns, col = current % columns;

            if (dx != 0)
            {
                int rowStart = row * columns;
                int rowLen = Mathf.Min(columns, count - rowStart);
                col = ((col + dx) % rowLen + rowLen) % rowLen;
                return rowStart + col;
            }

            if (dy != 0)
            {
                int rows = (count + columns - 1) / columns;
                for (int i = 0; i < rows; i++)
                {
                    row = ((row + dy) % rows + rows) % rows;
                    int candidate = row * columns + col;
                    if (candidate < count) return candidate;
                }
            }

            return current;
        }
    }
}
