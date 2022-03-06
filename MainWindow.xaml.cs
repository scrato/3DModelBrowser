using HelixToolkit.Wpf;
using ModelBrowser3D.Data;
using ModelBrowser3D.Data.Entities;
using ModelBrowser3D.Presentation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

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
            Database.Initialize();
        }



        bool _isClosing = false;
        const string c_ConfigFilename = "config.json";

        Settings Settings { get; set; }
        Database Database { get; } = new Database();
        SHA256 SHA256 { get; } = SHA256.Create();

        Material DefaultMaterial
        {
            get
            {
                var color = cPDefaultColor.SelectedColor ?? Color.FromRgb(66, 66, 66);
                return MaterialHelper.CreateMaterial(color);
            }
        }

        private ModelResult CurrentModel { get; set; }


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
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(3, GridUnitType.Star) });

            var viewPort = new HelixViewport3D();
            viewPort.ShowCameraInfo = true;
            viewPort.ShowCameraTarget = true;
            models.ForEach(model => model.ViewPort = viewPort);
            viewPort.RotateGesture = new MouseGesture(MouseAction.LeftClick);
            viewPort.SetValue(Grid.ColumnProperty, 1);
            viewPort.Camera = CameraHelper.CreateDefaultCamera();
            grid.Children.Add(viewPort);


            var list = new ListBox();
            models.ForEach(m =>
                {
                    var name = GetListBoxNameByModel(m);
                    var lb = new ListBoxItem() { Content = name, Tag = m };
                    m.ListBoxItem = lb;
                    list.Items.Add(lb);
                });
            list.SetValue(Grid.ColumnProperty, 0);
            list.SelectionChanged += List_SelectionChanged;
            grid.Children.Add(list);
            return grid;
        }

        private string GetListBoxNameByModel(ModelResult m) { return m.Entity == null ? m.Name : $"(x) {m.Name}"; }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            var lbItem = e.AddedItems[0] as ListBoxItem;
            if (lbItem == null) return;
            CurrentModel = lbItem.Tag as ModelResult;
            if (CurrentModel == null) return;

            RefreshCurrentModelInView();
            
        }

        private void RefreshCurrentModelInView()
        {
            CurrentModel.ViewPort.Children.Clear();
            var result = CurrentModel.Model.Value;
            if (!string.IsNullOrEmpty(result.Error))
            {
                System.Windows.MessageBox.Show($"An error uccured loading this model:{Environment.NewLine}{result.Error}");
            }
            var modelEntity = CurrentModel.Entity;
            var color = modelEntity?.Color;
            if (color != null) cPDefaultColor.SelectedColor = color.ToMediaColor();
            var device3D = new ModelVisual3D();
            device3D.Content = result.Model;

            var vp = CurrentModel.ViewPort;
            vp.Children.Add(new SunLight() { Brightness = 100, 
                Altitude = slAltitude.Value,
                Azimuth = slAzimuth.Value});
            vp.Children.Add(device3D);

            
            if (modelEntity != null)
            {
                vp.CameraController.CameraLookDirection = new Vector3D(modelEntity.LookDirectionX, modelEntity.LookDirectionY, modelEntity.LookDirectionZ);
                vp.CameraController.CameraPosition = new Point3D(modelEntity.CameraPositionX, modelEntity.CameraPositionY, modelEntity.CameraPositionZ);
                vp.CameraController.CameraTarget = new Point3D(modelEntity.CameraTargetX, modelEntity.CameraTargetY, modelEntity.CameraTargetZ);
                vp.CameraController.CameraUpDirection = new Vector3D(modelEntity.UpDirectionX, modelEntity.UpDirectionY, modelEntity.UpDirectionZ);

                slAltitude.Value = modelEntity.SunAltitude;
                slAzimuth.Value = modelEntity.SunAzimuth;
            }

        }

        

        private IEnumerable<ModelResult> Collect3DModels(string pathStr)
        {
            var files = System.IO.Directory.GetFiles(pathStr)
                .Where(f => f.EndsWith(".stl", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
            var result = files.Select(f => GetModelByPath(f)).ToList();
            return result;
        }

        private ModelResult GetModelByPath(string path)
        {
            var result = new ModelResult(path);

                var modelImp = new ModelImporter();
            
                result.Model =  new Lazy<ModelResult.ModelItem>(() =>
                    {
                        var model = new ModelResult.ModelItem();
                        try
                        {
                            model.Model = modelImp.Load(path, Dispatcher);
                            RefreshColorForChilden(model.Model);
                        }
                        catch (Exception ex) { model.Error = ex.Message; }
                        return model;
                    });
            result.Entity = Database.Models.FirstOrDefault(m => m.Path == path);
            return result;
        }


        private class ModelResult
        {
            public Lazy<ModelItem> Model { get; set; }
            public HelixViewport3D ViewPort { get; set; }
            public string Path { get; }
            public string Name { get; }
            public ModelEntity Entity { get; set; }
            public ListBoxItem ListBoxItem { get; internal set; }

            public ModelResult(string path)
            {
                Path = path;
                Name = System.IO.Path.GetFileNameWithoutExtension(Path);
            }

            public class ModelItem
            {
                public Model3DGroup Model { get; set; }
                public string Error { get; set; }
            }
        }

        private void CPDefaultColor_GotFocus(object sender, RoutedEventArgs e)
        {
            if(_RerouteFocusIfSet)
            {
                _RerouteFocusIfSet = false;
                CurrentModel?.ViewPort.Focus();
            }
        }

        bool _RerouteFocusIfSet = false;
        private void CPDefaultColor_Closed(object sender, RoutedEventArgs e)
        {
            _RerouteFocusIfSet = true;
        }



        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Settings = new Settings();
            if (File.Exists(c_ConfigFilename))
            {
                Settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(c_ConfigFilename));
                foreach (var tab in Settings.SelectedFolders)
                {
                    CreateTabByPath(tab);
                }
                cPDefaultColor.SelectedColor = Settings.SelectedColor.ToMediaColor();
                slAltitude.Value = Settings.SelectedAltitude;
                slAzimuth.Value = Settings.SelectedAzimuth;
            }
            if (tC.Items.Count == 1) AddNewPath();
            cPDefaultColor.Closed += CPDefaultColor_Closed;
            cPDefaultColor.GotFocus += CPDefaultColor_GotFocus;
        }


        private void Window_Closed(object sender, EventArgs e)
        {
            Settings.SelectedFolders.Clear();
            foreach (var tab in tC.Items.OfType<TabItem>().Except(new TabItem[] {tAddBrowser}))
            {
                Settings.SelectedFolders.Add((string)tab.Tag);
            }
            Settings.SelectedFolders.Reverse();
            Settings.SelectedColor = cPDefaultColor.SelectedColor.Value.ToHtml();
            Settings.SelectedAltitude = slAltitude.Value;
            Settings.SelectedAzimuth = slAzimuth.Value;

            File.WriteAllText(c_ConfigFilename, JsonConvert.SerializeObject(Settings));
            Database.SaveChanges();
        }


        private void AddOrUpdate_Click(object sender, RoutedEventArgs e)
        {
            AddOrUpdateCurrentModel();
        }

        private void AddOrUpdateCurrentModel()
        {
            if (CurrentModel == null) return;
            var folderPath = System.IO.Path.GetDirectoryName(CurrentModel.Path);

            var folder = Database.Folders.FirstOrDefault(f => f.Path == folderPath);
            if (folder == null)
            {
                folder = new FolderEntity();
                folder.Path = folderPath;
                folder.Name = System.IO.Path.GetFileName(folderPath);
                Database.Folders.Add(folder);
            }

            //Create Thumbnail
            var thumbnailPath = $@"{System.IO.Path.GetDirectoryName(CurrentModel.Path)}\{System.IO.Path.GetFileNameWithoutExtension(CurrentModel.Path)}.png";
            var vp = CurrentModel.ViewPort;
            vp.Viewport.SaveBitmap(thumbnailPath);

            var model = Database.Models.FirstOrDefault(m => m.Path == CurrentModel.Path);
            if (model == null)
            {
                model = new ModelEntity();
                model.Path = CurrentModel.Path;
                Database.Models.Add(model);
            }
            model.Hashcode = SHA256.ComputeHash(File.OpenRead(CurrentModel.Path));
            model.Name = System.IO.Path.GetFileName(CurrentModel.Path);
            model.Folder = folder;
                        
            model.ThumbnailPath = thumbnailPath;


            model.LookDirectionX = vp.CameraController.CameraLookDirection.X;
            model.LookDirectionY = vp.CameraController.CameraLookDirection.Y;
            model.LookDirectionZ = vp.CameraController.CameraLookDirection.Z;

            model.UpDirectionX = vp.CameraController.CameraUpDirection.X;
            model.UpDirectionY = vp.CameraController.CameraUpDirection.Y;
            model.UpDirectionZ = vp.CameraController.CameraUpDirection.Z;

            model.CameraTargetX = vp.CameraController.CameraTarget.X;
            model.CameraTargetY = vp.CameraController.CameraTarget.Y;
            model.CameraTargetZ = vp.CameraController.CameraTarget.Z;

            model.CameraPositionX = vp.CameraController.CameraPosition.X;
            model.CameraPositionY = vp.CameraController.CameraPosition.Y;
            model.CameraPositionZ = vp.CameraController.CameraPosition.Z;

            model.Color = cPDefaultColor.SelectedColor.Value.ToHtml();
            model.SunAltitude = slAltitude.Value;
            model.SunAzimuth = slAzimuth.Value;

             
            Database.SaveChanges();
            CurrentModel.Entity = model;
            CurrentModel.ListBoxItem.Content = GetListBoxNameByModel(CurrentModel);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter && !cPDefaultColor.IsOpen)
            {
                if (CurrentModel == null) return;
                AddOrUpdateCurrentModel();

                var lb = CurrentModel.ListBoxItem.Parent as ListBox;
                if (lb == null) return;

                //Nächstes Element auswählen
                var index = lb.SelectedIndex;
                var nextIndex = (index + 1) % (lb.Items.Count);
                lb.SelectedIndex = nextIndex;

                e.Handled = true;
            }
        }

        private void ColorPicker_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
          if(CurrentModel != null)
            {
                RefreshColorForChilden(CurrentModel.Model.Value.Model);
            }
        }

        private void RefreshColorForChilden(Model3DGroup model, string color = null)
        {
            var material = (color == null) ? DefaultMaterial : MaterialHelper.CreateMaterial(color.ToMediaColor());
            model.Children.ForEach(child => child.SetValue(GeometryModel3D.MaterialProperty, material));
        }

        private void Altitude_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbAltitude == null) return;
            tbAltitude.Text = $"{Math.Round(slAltitude.Value,2)}°";
            CurrentModel?.ViewPort.Children.OfType<SunLight>().ForEach(light => light.Altitude = slAltitude.Value);
        }

        private void Azimuth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbAzimuth == null) return;
            tbAzimuth.Text = $"{Math.Round(slAzimuth.Value, 2)}°";
            CurrentModel?.ViewPort.Children.OfType<SunLight>().ForEach(light => light.Azimuth = slAzimuth.Value);
        }
    }
}
