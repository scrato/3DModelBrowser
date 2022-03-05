using HelixToolkit.Wpf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace _3DModelBrowser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        bool _isClosing = false;
        const string c_ConfigFilename = "config.json";

        Settings Settings { get; set; }

        private void ButtonAddNewTab_Clicked(object sender, RoutedEventArgs e)
        {
            AddNewPath();
        }
        private void AddNewPath()
        {
            if (_isClosing) return;
            var pathDialog = new System.Windows.Forms.FolderBrowserDialog();
            pathDialog.SelectedPath = Directory.Exists(Settings.LastPath) ? Settings.LastPath : String.Empty;
            if (pathDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var pathStr = pathDialog.SelectedPath;
                Settings.LastPath = pathStr;
                CreateTabByPathRecursive(pathStr);
            }
            else
            {
                if (tC.Items.Count == 1) Close();
                _isClosing = true;
            }
        }

        private void CreateTabByPathRecursive(string path)
        {
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                CreateTabByPathRecursive(dir);
            }
            CreateTabByPath(path);
        }

        private void CreateTabByPath(string path)
        {
            var folderName = System.IO.Path.GetFileName(path);

            var items = Collect3DModels(path);
            var newTab = new TabItem() { Header = new TextBlock() { Text = folderName } };
            newTab.Content = CreateTabContent(items);
            newTab.Tag = path;
            ((UIElement) newTab.Header).MouseUp += NewTab_MouseUp;

            tC.Items.Insert(0, newTab);
            tC.SelectedIndex = 0;
        }

        private void NewTab_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var item = sender as TextBlock;
            if (e.ChangedButton == MouseButton.Middle && sender != tAddBrowser) tC.Items.Remove(item.Parent);
        }

        private object CreateTabContent(IEnumerable<ModelResult> models)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star)});
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(3, GridUnitType.Star) });

            var viewPort = new HelixViewport3D();
            //viewPort.ShowCameraInfo = true;
            //viewPort.ShowCameraTarget = true;
            models.ForEach(model => model.ViewPort = viewPort);
            viewPort.RotateGesture = new MouseGesture(MouseAction.LeftClick);
            viewPort.SetValue(Grid.ColumnProperty, 1);
            grid.Children.Add(viewPort);    


            var list = new ListBox();
            models.ForEach(m => list.Items.Add(new ListBoxItem() { Content = m.Name, Tag = m }));
            list.SetValue(Grid.ColumnProperty, 0);
            list.SelectionChanged += differentModelSelected;
            grid.Children.Add(list);


            return grid;
        }

        private void differentModelSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            var lbItem = e.AddedItems[0] as ListBoxItem;
            if (lbItem == null) return;
            var model = lbItem.Tag as ModelResult;
            if (model == null) return;
            model.ViewPort.Children.Clear();

            var result = model.Model.Value;
            if(!string.IsNullOrEmpty(result.Error))
            {
                System.Windows.MessageBox.Show($"An error uccured loading this model:{Environment.NewLine}{result.Error}");
            }

            var device3D = new ModelVisual3D();
            device3D.Content = result.Model;
            var light = new DefaultLights();
            model.ViewPort.Children.Add(new DefaultLights());
            model.ViewPort.Children.Add(device3D);
            model.ViewPort.CameraController.CameraLookDirection = new Vector3D(475.460,380.018,-303.976);
            model.ViewPort.CameraController.CameraPosition = new Point3D(-314.972, -307.695, 237.760);
            model.ViewPort.CameraController.CameraTarget = new Point3D(160.489,72.322,-66.216);
        }

        private IEnumerable<ModelResult> Collect3DModels(string pathStr)
        {
            var files = System.IO.Directory.GetFiles(pathStr)
                .Where(f => f.EndsWith(".stl", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
            var result = files.Select(f => GetModelByPath(f)).ToList();
            return result;
        }

        private ModelResult GetModelByPath(string f)
        {
            var result = new ModelResult(f);

                var modelImp = new ModelImporter();
            
                result.Model =  new Lazy<ModelResult.ModelItem>(() =>
                    {
                        var model = new ModelResult.ModelItem();
                        try { model.Model = modelImp.Load(f, Dispatcher);}
                        catch (Exception ex) { model.Error = ex.Message;}
                        return model;
                    });
            return result;
        }


        private class ModelResult
        {
            public Lazy<ModelItem> Model { get; set; }
            public HelixViewport3D ViewPort { get; set; }
            public string Path { get; }
            public string Name { get; }

            public ModelResult(string path)
            {
                Path = path;
                Name = System.IO.Path.GetFileNameWithoutExtension(Path);
            }

            public class ModelItem
            {
                public Model3D Model { get; set; }
                public string Error { get; set; }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Settings = new Settings();
            if(File.Exists(c_ConfigFilename))
            {
                Settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(c_ConfigFilename));
                foreach (var tab in Settings.SelectedFolders)
                {
                    CreateTabByPath(tab);
                }
            }
            if(tC.Items.Count == 1) AddNewPath();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Settings.SelectedFolders.Clear();
            foreach (var tab in tC.Items.OfType<TabItem>().Except(new TabItem[] {tAddBrowser}))
            {
                Settings.SelectedFolders.Add((string)tab.Tag);
            }
            Settings.SelectedFolders.Reverse();
            File.WriteAllText(c_ConfigFilename, JsonConvert.SerializeObject(Settings));
        }
    }
}
