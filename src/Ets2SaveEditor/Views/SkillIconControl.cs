using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace Ets2SaveEditor.Views
{
    /// <summary>
    /// 一个驾驶技能的图标组：横向排开 6 枚图标，点亮的枚数即该技能的等级（0~6）。
    /// <para>
    /// 点击图标可切换该位的亮灭——点击第 n 枚图标时，若前 n 枚已全部点亮，
    /// 则把等级设为 n（点满这枚）；否则把等级设为 n（点亮到这一枚）。
    /// 因此既能逐级点亮，也能直接点最远的一枚跳级。
    /// </para>
    /// </summary>
    public class SkillIconControl : ContentControl
    {
        /// <summary>技能名称，显示在图标组上方。</summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title), typeof(string), typeof(SkillIconControl),
                new PropertyMetadata(string.Empty, OnVisualChanged));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        /// <summary>当前等级（0~6）。</summary>
        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register(
                nameof(Level), typeof(int), typeof(SkillIconControl),
                new PropertyMetadata(0, OnVisualChanged));

        public int Level
        {
            get { return (int)GetValue(LevelProperty); }
            set { SetValue(LevelProperty, Math.Max(0, Math.Min(6, value))); }
        }

        /// <summary>
        /// 技能编号（0~4），决定使用哪一组图标几何。
        /// </summary>
        public static readonly DependencyProperty SkillIndexProperty =
            DependencyProperty.Register(
                nameof(SkillIndex), typeof(int), typeof(SkillIconControl),
                new PropertyMetadata(0, OnVisualChanged));

        public int SkillIndex
        {
            get { return (int)GetValue(SkillIndexProperty); }
            set { SetValue(SkillIndexProperty, value); }
        }

        // 5 个技能各自的图标形状（Path Geometry，坐标系 0~24）。同一技能的 6 枚
        // 重复同一形状，只是「点亮 / 熄灭」两种状态。
        private static readonly string[][] IconPaths = new[]
        {
            // 0 长途运输：公路
            new[]
            {
                "M 2,16 L 7,16 L 9,9 L 15,9 L 17,16 L 22,16",
                "M 2,20 L 22,20",
                "M 3,12 L 6,12",
                "M 18,12 L 21,12",
            },
            // 1 重载货物：货箱
            new[]
            {
                "M 3,9 L 21,9 L 21,19 L 3,19 Z",
                "M 3,9 L 12,3 L 21,9",
                "M 12,3 L 12,9",
                "M 3,19 L 21,19",
            },
            // 2 易碎品：玻璃杯
            new[]
            {
                "M 6,6 L 18,6 L 16,20 L 8,20 Z",
                "M 5,4 L 19,4",
                "M 10,9 L 10,17",
                "M 14,9 L 14,17",
            },
            // 3 高时效：闪电
            new[]
            {
                "M 13,2 L 6,13 L 11,13 L 10,22 L 18,10 L 13,10 Z",
                "M 3,6 L 6,6",
                "M 18,18 L 21,18",
                "M 3,18 L 6,18",
            },
            // 4 节油驾驶：叶片
            new[]
            {
                "M 12,3 C 18,8 18,16 12,21 C 6,16 6,8 12,3 Z",
                "M 12,3 L 12,21",
                "M 12,9 C 15,11 15,15 12,17",
                "M 12,15 C 9,13 9,9 12,7",
            },
        };

        private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SkillIconControl)d).Rebuild();
        }

        private readonly StackPanel _row = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly TextBlock _title = new TextBlock();
        private readonly TextBlock _levelText = new TextBlock();

        public SkillIconControl()
        {
            _title.FontSize = 12;
            _title.Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x2D, 0x3D));
            _title.Margin = new Thickness(0, 0, 0, 3);

            _levelText.FontSize = 13;
            _levelText.FontWeight = FontWeights.Bold;
            _levelText.VerticalAlignment = VerticalAlignment.Center;
            _levelText.Margin = new Thickness(10, 0, 0, 0);
            _levelText.Foreground = new SolidColorBrush(Color.FromRgb(0x14, 0x54, 0x6B));

            var left = new StackPanel { Orientation = Orientation.Vertical };
            left.Children.Add(_title);
            left.Children.Add(_row);

            var host = new StackPanel { Orientation = Orientation.Horizontal };
            host.Children.Add(left);
            host.Children.Add(_levelText);

            Content = host;
            Rebuild();
        }

        private void Rebuild()
        {
            _row.Children.Clear();

            int level = Math.Max(0, Math.Min(6, Level));
            int skill = Math.Max(0, Math.Min(IconPaths.Length - 1, SkillIndex));
            var paths = IconPaths[skill];

            var lit = new SolidColorBrush(Color.FromRgb(0x14, 0x8F, 0x8F));
            var dark = new SolidColorBrush(Color.FromRgb(0xE2, 0xE9, 0xEE));

            for (int i = 0; i < 6; i++)
            {
                bool on = i < level;

                // 一个图标由若干条路径叠成，放进 Grid 里重叠绘制。
                var box = new Grid
                {
                    Width = 30,
                    Height = 30,
                    Margin = new Thickness(2, 0, 2, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = on ? "点击可取消这一级" : "点击可点亮到这一级",
                };

                foreach (string d in paths)
                {
                    // 注意：Geometry.Parse 返回的对象是只读的，不能设置 Transform。
                    // 缩放要通过 Path 自身的 RenderTransform 来做。
                    var scale = new ScaleTransform(0.86, 0.86);
                    scale.CenterX = 12;
                    scale.CenterY = 12;

                    box.Children.Add(new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse(d),
                        RenderTransform = scale,
                        Stroke = on ? lit : dark,
                        StrokeThickness = on ? 1.8 : 1.3,
                        StrokeEndLineCap = PenLineCap.Round,
                        StrokeLineJoin = PenLineJoin.Round,
                        Fill = Brushes.Transparent,
                        Stretch = Stretch.Uniform,
                        Width = 30,
                        Height = 30,
                    });
                }

                int index = i;
                box.MouseLeftButtonUp += (s, e) =>
                {
                    // 点第 n 枚：已点满这枚则退回 n-1，否则点亮到 n。
                    Level = (Level == index + 1) ? index : index + 1;
                };

                _row.Children.Add(box);
            }

            _levelText.Text = Level + "/6";
            _title.Text = Title;
        }
    }
}
