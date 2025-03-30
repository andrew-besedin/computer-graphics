using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Лаб1WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Obj? obj = null;
        Renderer renderer = new Renderer();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new();
            if (openFileDialog.ShowDialog() == true)
            {
                string filename = openFileDialog.FileName;

                var obj = Parser.ParseObjFile(filename);

                this.obj = obj;

                this.Draw();
            }
        }

        private void Draw()
        {
            var width = grid.ActualWidth;
            var height = grid.ActualHeight;

            var bitmap = new WriteableBitmap((int)width, (int)height, 96, 96, PixelFormats.Bgra32, null);




            //var graphics = Graphics.FromImage(bitmap);
            //graphics.Clear(Color.Black);

            if (obj != null)
            {
                renderer.render(bitmap, obj);
            }

            image.Source = bitmap;

            //bitmap.Save("myimg.png", ImageFormat.Png);

            //pictureBox1.Refresh();
            //e.Graphics.DrawImage(bitmap, 0, 0);

            //pictureBox1.Size = new Size(210, 110);

            //Bitmap flag = new Bitmap(200, 100);
            //Graphics flagGraphics = Graphics.FromImage(flag);
            //int red = 0;
            //int white = 11;
            //while (white <= 100)
            //{
            //    flagGraphics.FillRectangle(Brushes.Red, 0, red, 200, 10);
            //    flagGraphics.FillRectangle(Brushes.White, 0, white, 200, 10);
            //    red += 20;
            //    white += 20;
            //}
            //pictureBox1.Image = flag;
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            prevPosInited = false;

            CaptureMouse();
        }

        private Point prevPosition;
        private bool prevPosInited = false;

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!IsMouseCaptured)
            {
                return;
            }

            var newPos = e.GetPosition(grid);

            if (!prevPosInited)
            {
                prevPosition = newPos;
                prevPosInited = true;
                return;
            }

            double deltaX = newPos.X - prevPosition.X;
            double deltaY = newPos.Y - prevPosition.Y;

            prevPosition = newPos;

            renderer.cameraAngleX -= (float)deltaX / 10.0f;
            renderer.cameraAngleY += (float)deltaY / 10.0f;
            this.Draw();

        }

        private void Window_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            prevPosInited = false;

            ReleaseMouseCapture();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.A)
            {
                renderer.cameraSphereRadius = Math.Max(renderer.cameraSphereRadius - 1, 1);
            }

            if (e.Key == Key.Z)
            {
                renderer.cameraSphereRadius += 1;
            }

            if (e.Key == Key.S)
            {
                renderer.cameraSphereRadius = Math.Max(renderer.cameraSphereRadius - 100, 1);
            }

            if (e.Key == Key.X)
            {
                renderer.cameraSphereRadius += 100;
            }

            this.Draw();
        }

        int frameCount = 0;
        Stopwatch stopwatch = new Stopwatch();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            CompositionTarget.Rendering += (o, e) =>
            {
                if (stopwatch.ElapsedTicks >= 10_000_000)
                {
                    fpsLabel.Content = $"{frameCount} FPS";
                    frameCount = 0;
                }

                if (frameCount++ == 0)
                {
                    stopwatch.Restart();
                }

                if (obj != null)
                {
                    this.Draw();
                    renderer.cameraAngleX += 0.1f;
                }
            };
        }
    }
}