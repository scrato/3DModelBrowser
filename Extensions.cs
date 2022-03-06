using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelBrowser3D.Presentation
{
    internal static class Extensions
    {
        /// <summary>
        /// Executes the action for every item in the Enumerable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="action"></param>
        public static void ForEach<T>(this IEnumerable<T> items, Action<T> action)
        {
            foreach (var item in items) action(item);
        }

        public static System.Drawing.Color ChangeType(this System.Windows.Media.Color color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static System.Windows.Media.Color ChangeType(this System.Drawing.Color color)
        {
            return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static string ToHtml(this System.Windows.Media.Color color)
        {
            return ColorTranslator.ToHtml(color.ChangeType());
        }

        public static System.Windows.Media.Color ToMediaColor(this string color)
        {
            return ColorTranslator.FromHtml(color).ChangeType();
        }
    }
}
