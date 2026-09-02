using System.ComponentModel;
using System.Drawing;
using AutoTestClient.Models;
using AutoTestClient.Recipes;
using AutoTestClient.Settings;

namespace AutoTestClient;

/// <summary>可在设计器调整字段、运行时维护测试项目和固定图卡队列。</summary>
public partial class RecipeManagerForm : Form
{
    private static readonly HashSet<string> SupportedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".bmp", ".jpg", ".jpeg", ".tif", ".tiff"
        };

    private readonly SettingsStore _store;
    private readonly bool _isDesignTime;
    private TestProject? _boundProject;
    private bool _cancelRequested;
    private bool _updatingBindingUi;
    // DataGridView raises SelectionChanged several times while its DataSource
    // is being detached/attached.  Keep the editor update atomic so a stale
    // row index cannot be used while the grid is in that transient state.
    private bool _rebindingGrids;
    private List<string> _availableImageFiles = new();
    private string _availableImageDirectory = string.Empty;
    public TestPlanConfiguration Configuration { get; private set; }

    /// <summary>
    /// ComboBox 中显示的配方项。显示名和实际绑定路径分开保存，
    /// 避免依靠用户手工输入名称来定位文件。
    /// </summary>
    private sealed record RecipeChoice(string Name, string FilePath)
    {
        public override string ToString() => Name;
    }

    /// <summary>ComboBox 中显示的图卡项；Path 始终保存绝对路径。</summary>
    private sealed record ImageChoice(string Name, string Path, bool Missing = false)
    {
        public override string ToString() => Missing ? $"{Name}（文件不存在）" : Name;
    }

    /// <summary>
    /// Visual Studio WinForms Designer 使用的无参入口。
    /// 设计器不应读取用户配置或绑定运行时数据，所以通过显式标记走预览路径。
    /// </summary>
    public RecipeManagerForm()
        : this(new TestPlanConfiguration(), new SettingsStore(), designTime: true)
    {
    }

    public RecipeManagerForm(TestPlanConfiguration configuration, SettingsStore store)
        : this(configuration, store, designTime: false)
    {
    }

    private RecipeManagerForm(TestPlanConfiguration configuration, SettingsStore store, bool designTime)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(store);
        Configuration = Clone(configuration);
        _store = store;
        _isDesignTime = designTime || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
        InitializeComponent();
        // gridSteps 的事件由代码挂接，避免设计器保存时遗漏；绑定面板的
        // 事件由 Designer.cs 挂接，便于在 Visual Studio 设计器中查看和调整。
        if (gridSteps is not null)
        {
            gridSteps.SelectionChanged += GridSteps_SelectionChanged;
            gridSteps.CellEndEdit += GridSteps_CellEndEdit;
        }
        if (gridProjects is not null)
            gridProjects.CellEndEdit += GridProjects_CellEndEdit;
        if (_isDesignTime)
        {
            // 只显示控件和列标题，不访问设置文件，也不触发运行时 DataSource 绑定。
            labelHint.Text = "设计器预览：可在此调整项目、图卡和按钮布局。运行时打开后加载测试计划。";
            SetBindingLabels(null, null);
        }
        else
        {
            Load += (_, _) => BindProjects();
            FormClosing += RecipeManagerForm_FormClosing;
            FormClosed += (_, _) => ClearImagePreview();
        }
    }

    private void BindProjects(int preferredIndex = -1)
    {
        if (_isDesignTime || _rebindingGrids) return;

        _rebindingGrids = true;
        try
        {
            CommitBindingEdits();
            CommitStepGrid(_boundProject);
            CommitProjectGrid();
            Configuration.Normalize();

            // Existing plans created before the binding panel may have a recipe
            // file path but no protocol name.  Fill only that missing default on
            // load; a non-empty name may be an intentional MRTEST override and is
            // therefore preserved until the user chooses a different file.
            foreach (TestProject project in Configuration.Projects)
            {
                if (string.IsNullOrWhiteSpace(project.RecipeName) &&
                    !string.IsNullOrWhiteSpace(project.RecipeFilePath))
                {
                    ApplyRecipeNameFromFile(project, project.RecipeFilePath);
                }
            }

            // A List<T> does not notify DataGridView when an item is removed.
            // Detach and attach while the rebind guard is held; otherwise the
            // intermediate SelectionChanged events can read a row index that no
            // longer exists and overwrite the wrong project's image path.
            gridSteps.DataSource = null;
            gridProjects.DataSource = null;
            gridProjects.DataSource = Configuration.Projects;

            int selectedIndex = ResolveProjectSelectionIndex(preferredIndex);
            if (selectedIndex >= 0 && selectedIndex < gridProjects.Rows.Count &&
                gridProjects.Columns.Count > 0)
            {
                gridProjects.CurrentCell = gridProjects.Rows[selectedIndex].Cells[0];
                gridProjects.Rows[selectedIndex].Selected = true;
                Configuration.SelectedProjectIndex = selectedIndex;
            }
            else
            {
                Configuration.SelectedProjectIndex = 0;
            }

            // SelectionChanged is intentionally ignored while the grids are
            // being rebuilt; perform one deterministic bind after both sources
            // are ready.
            BindSelectedStepsCore(commitEdits: false);
        }
        finally
        {
            _rebindingGrids = false;
        }
    }

    private int ResolveProjectSelectionIndex(int preferredIndex)
    {
        if (gridProjects.Rows.Count == 0 || Configuration.Projects.Count == 0)
            return -1;

        int candidate = preferredIndex >= 0
            ? preferredIndex
            : Configuration.SelectedProjectIndex;
        return Math.Clamp(candidate, 0, Math.Min(gridProjects.Rows.Count, Configuration.Projects.Count) - 1);
    }

    private void BindSelectedSteps()
    {
        if (_isDesignTime || _rebindingGrids) return;

        _rebindingGrids = true;
        try
        {
            BindSelectedStepsCore(commitEdits: true);
        }
        finally
        {
            _rebindingGrids = false;
        }
    }

    private void BindSelectedStepsCore(bool commitEdits)
    {
        if (commitEdits)
        {
            CommitBindingEdits();
            CommitStepGrid(_boundProject);
            CommitProjectGrid();
        }

        TestProject? selectedProject = GetSelectedProject();
        _boundProject = selectedProject;
        if (selectedProject is null)
        {
            gridSteps.DataSource = null;
            ShowSelectedBindings();
            return;
        }

        gridSteps.DataSource = null;
        gridSteps.DataSource = selectedProject.Steps;
        if (gridSteps.Rows.Count > 0)
        {
            gridSteps.CurrentCell = gridSteps.Rows[0].Cells[0];
            gridSteps.Rows[0].Selected = true;
        }
        ShowSelectedBindings();
    }

    private void GridProjects_SelectionChanged(object? sender, EventArgs e)
    {
        if (_rebindingGrids) return;
        TestProject? selected = GetSelectedProject();
        int selectedIndex = selected is null ? -1 : Configuration.Projects.IndexOf(selected);
        if (selectedIndex >= 0)
        {
            Configuration.SelectedProjectIndex = selectedIndex;
        }
        BindSelectedSteps();
    }

    /// <summary>
    /// 直接编辑项目表中的“配方文件(可选)”时，立即把 MRTEST 配方名
    /// 自动更新为文件名（不含扩展名）。配方名单元格本身仍保持可编辑，
    /// 因此用户随后可以按设备实际名称手工修正；只有再次更换配方文件时
    /// 才会重新按新文件名自动填充。
    /// </summary>
    private void GridProjects_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_isDesignTime || _rebindingGrids || e.RowIndex < 0 ||
            e.RowIndex >= gridProjects.Rows.Count || e.ColumnIndex < 0 ||
            e.ColumnIndex >= gridProjects.Columns.Count)
            return;

        DataGridViewRow row = gridProjects.Rows[e.RowIndex];
        if (row.DataBoundItem is not TestProject project ||
            !Configuration.Projects.Contains(project))
            return;

        string columnName = gridProjects.Columns[e.ColumnIndex].Name;
        if (columnName.Equals("RecipeFilePath", StringComparison.OrdinalIgnoreCase))
        {
            string path = CellText(row, "RecipeFilePath", project.RecipeFilePath).Trim();
            project.RecipeFilePath = path;
            ApplyRecipeNameFromFile(project, path);
            UpdateProjectGridRow(project);

            // Keep the right-side binding controls in sync when the edited row
            // is the current project.  The guard prevents this refresh from
            // recursively treating the programmatic cell updates as edits.
            if (ReferenceEquals(_boundProject, project))
                ShowSelectedBindings();
        }
        else if (columnName.Equals("RecipeName", StringComparison.OrdinalIgnoreCase))
        {
            project.RecipeName = CellText(row, "RecipeName", project.RecipeName).Trim();
            if (ReferenceEquals(_boundProject, project) && textBoxBoundRecipeName is not null)
            {
                _updatingBindingUi = true;
                try { textBoxBoundRecipeName.Text = project.RecipeName; }
                finally { _updatingBindingUi = false; }
            }
            UpdateProjectGridRow(project);
        }
    }

    private void GridSteps_SelectionChanged(object? sender, EventArgs e)
    {
        if (_isDesignTime || _updatingBindingUi || _rebindingGrids) return;
        // Commit direct grid edits before refreshing the binding panel.  This
        // makes the path label/preview deterministic even when the click that
        // selects a row also ends an edit in the previous row.
        CommitStepGrid(_boundProject);
        ShowSelectedBindings();
    }

    private void GridSteps_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_isDesignTime || _rebindingGrids || e.RowIndex < 0 ||
            e.RowIndex >= gridSteps.Rows.Count || e.ColumnIndex < 0 ||
            e.ColumnIndex >= gridSteps.Columns.Count)
            return;

        DataGridViewRow row = gridSteps.Rows[e.RowIndex];
        if (row.DataBoundItem is not TestStep step ||
            _boundProject is null || !_boundProject.Steps.Contains(step)) return;

        // DataGridView is bound to List<T>; explicitly copy the edited path so
        // the model, the lower-left grid cell, and the right preview all share
        // the same value without relying on a binding notification.
        step.ImagePath = CellText(row, "ImagePath", step.ImagePath).Trim();
        UpdateStepGridRow(step);
        if (ReferenceEquals(GetSelectedStep(), step))
        {
            SetBindingLabels(_boundProject, step);
            ShowImagePreview(step.ImagePath);
            RefreshBindingChoices();
        }
    }

    #region 配方/图卡选择绑定与预览

    /// <summary>
    /// 刷新两个绑定下拉框的候选项。该方法只在运行时扫描目录；
    /// 设计器路径不访问磁盘，避免 DesignToolsServer 加载失败。
    /// </summary>
    private void RefreshBindingChoices()
    {
        if (_isDesignTime) return;

        TestProject? project = _boundProject;
        TestStep? step = GetSelectedStep();
        _updatingBindingUi = true;
        try
        {
            PopulateRecipeChoices(project);
            PopulateImageChoices(step);
        }
        finally
        {
            _updatingBindingUi = false;
        }
    }

    /// <summary>把当前项目、图卡步骤同步到右侧绑定面板并更新预览。</summary>
    private void ShowSelectedBindings()
    {
        if (_isDesignTime)
        {
            SetBindingLabels(null, null);
            return;
        }

        TestProject? project = _boundProject;
        TestStep? step = GetSelectedStep();
        _updatingBindingUi = true;
        try
        {
            SetBindingLabels(project, step);
            if (textBoxBoundRecipeName is not null)
                textBoxBoundRecipeName.Text = project?.RecipeName ?? string.Empty;
        }
        finally
        {
            _updatingBindingUi = false;
        }

        RefreshBindingChoices();
        ShowImagePreview(step?.ImagePath);
    }

    /// <summary>
    /// 保存前提交绑定面板中的编辑值。网格仍是兼容入口，
    /// 因此随后还会由 CommitProjectGrid/CommitStepGrid 提交其单元格值。
    /// </summary>
    private void CommitBindingEdits()
    {
        if (_isDesignTime) return;

        // Finish an in-place grid edit before reading the side-panel controls.
        // Without this ordering, a user who types a path in the lower grid and
        // immediately presses Save/Choose can have the still-selected (old)
        // combo-box item write the old path back over the new cell value.
        CommitProjectGrid();
        CommitStepGrid(_boundProject);

        TestProject? project = _boundProject;
        if (project is not null)
        {
            if (textBoxBoundRecipeName is not null)
                project.RecipeName = textBoxBoundRecipeName.Text.Trim();
            if (comboBoxRecipeFile?.SelectedItem is RecipeChoice recipeChoice)
            {
                string oldPath = ResolveRecipeFilePath(project.RecipeFilePath);
                project.RecipeFilePath = recipeChoice.FilePath;
                if (recipeChoice.FilePath.Length > 0 &&
                    (!recipeChoice.FilePath.Equals(oldPath, StringComparison.OrdinalIgnoreCase) ||
                     string.IsNullOrWhiteSpace(project.RecipeName)))
                {
                    ApplyRecipeNameFromFile(project, recipeChoice.FilePath);
                }
            }
            UpdateProjectGridRow(project);
        }

        TestStep? step = GetSelectedStep();
        if (step is not null && comboBoxStepImage?.SelectedItem is ImageChoice imageChoice)
        {
            step.ImagePath = imageChoice.Path;
            UpdateStepGridRow(step);
        }
    }

    private void PopulateRecipeChoices(TestProject? project)
    {
        if (comboBoxRecipeFile is null) return;

        string currentPath = ResolveRecipeFilePath(project?.RecipeFilePath);
        var choices = new List<RecipeChoice>
        {
            new("（未绑定配方）", string.Empty)
        };

        try
        {
            foreach (RecipeEntry entry in RecipeCatalog.Discover(Configuration.RecipeDirectory))
            {
                string path = NormalizeFullPath(entry.FilePath);
                if (path.Length == 0 || choices.Any(choice =>
                        choice.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                choices.Add(new RecipeChoice(entry.Name, path));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 目录不可读时仍保留当前已绑定路径，并让用户用“选择文件”修复。
            AppendBindingLog($"读取配方目录失败：{ex.Message}");
        }

        if (currentPath.Length > 0 && !choices.Any(choice =>
                choice.FilePath.Equals(currentPath, StringComparison.OrdinalIgnoreCase)))
        {
            string displayName = Path.GetFileNameWithoutExtension(currentPath);
            choices.Add(new RecipeChoice(
                displayName.Length == 0 ? currentPath : $"{displayName}（当前绑定）",
                currentPath));
        }

        comboBoxRecipeFile.BeginUpdate();
        try
        {
            comboBoxRecipeFile.Items.Clear();
            foreach (RecipeChoice choice in choices)
                comboBoxRecipeFile.Items.Add(choice);

            int selected = choices.FindIndex(choice =>
                choice.FilePath.Equals(currentPath, StringComparison.OrdinalIgnoreCase));
            comboBoxRecipeFile.SelectedIndex = selected >= 0 ? selected : 0;
        }
        finally
        {
            comboBoxRecipeFile.EndUpdate();
        }
    }

    private void PopulateImageChoices(TestStep? step)
    {
        if (comboBoxStepImage is null) return;

        string currentPath = ResolveImagePath(step?.ImagePath);
        var choices = new List<ImageChoice>
        {
            new("（未绑定图卡）", string.Empty)
        };

        try
        {
            string imageDirectory = NormalizeFullPath(Configuration.ImageDirectory);
            // 图卡目录可能包含串扰的大量图片；只在目录配置变化或尚未
            // 建立缓存时扫描，避免每次单击步骤都卡住界面。
            if (!_availableImageDirectory.Equals(imageDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _availableImageFiles = DiscoverImageFiles(Configuration.ImageDirectory);
                _availableImageDirectory = imageDirectory;
            }
            foreach (string path in _availableImageFiles)
            {
                string fullPath = NormalizeFullPath(path);
                if (fullPath.Length == 0 || choices.Any(choice =>
                        choice.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                choices.Add(new ImageChoice(GetImageDisplayName(fullPath), fullPath));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _availableImageFiles = new List<string>();
            _availableImageDirectory = string.Empty;
            AppendBindingLog($"读取图卡目录失败：{ex.Message}");
        }

        if (currentPath.Length > 0 && !choices.Any(choice =>
                choice.Path.Equals(currentPath, StringComparison.OrdinalIgnoreCase)))
        {
            choices.Add(new ImageChoice(
                GetImageDisplayName(currentPath) + "（当前绑定）", currentPath, Missing: true));
        }

        comboBoxStepImage.BeginUpdate();
        try
        {
            comboBoxStepImage.Items.Clear();
            foreach (ImageChoice choice in choices)
                comboBoxStepImage.Items.Add(choice);

            int selected = choices.FindIndex(choice =>
                choice.Path.Equals(currentPath, StringComparison.OrdinalIgnoreCase));
            comboBoxStepImage.SelectedIndex = selected >= 0 ? selected : 0;
        }
        finally
        {
            comboBoxStepImage.EndUpdate();
        }
    }

    private void ComboBoxRecipeFile_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isDesignTime || _updatingBindingUi || _boundProject is null ||
            comboBoxRecipeFile?.SelectedItem is not RecipeChoice choice)
        {
            return;
        }

        string oldPath = ResolveRecipeFilePath(_boundProject.RecipeFilePath);
        _boundProject.RecipeFilePath = choice.FilePath;

        // 绑定面板选择了新文件时，按文件名（不含扩展名）自动填充协议
        // 配方名。填充后文本框和项目表仍可直接编辑，手工名称会一直保留
        // 到下一次更换配方文件。
        if (choice.FilePath.Length > 0 &&
            (!choice.FilePath.Equals(oldPath, StringComparison.OrdinalIgnoreCase) ||
             string.IsNullOrWhiteSpace(_boundProject.RecipeName)))
        {
            ApplyRecipeNameFromFile(_boundProject, choice.FilePath);
        }

        if (textBoxBoundRecipeName is not null)
            textBoxBoundRecipeName.Text = _boundProject.RecipeName;
        UpdateProjectGridRow(_boundProject);
        SetBindingLabels(_boundProject, GetSelectedStep());
    }

    private void TextBoxBoundRecipeName_TextChanged(object? sender, EventArgs e)
    {
        if (_isDesignTime || _updatingBindingUi || _boundProject is null ||
            textBoxBoundRecipeName is null)
        {
            return;
        }

        _boundProject.RecipeName = textBoxBoundRecipeName.Text.Trim();
        UpdateProjectGridRow(_boundProject);
        SetBindingLabels(_boundProject, GetSelectedStep());
    }

    private void ComboBoxStepImage_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isDesignTime || _updatingBindingUi ||
            GetSelectedStep() is not { } step ||
            comboBoxStepImage?.SelectedItem is not ImageChoice choice)
        {
            return;
        }

        step.ImagePath = choice.Path;
        UpdateStepGridRow(step);
        SetBindingLabels(_boundProject, step);
        ShowImagePreview(step.ImagePath);
    }

    private void ButtonChooseProjectRecipe_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime || _boundProject is null) return;

        CommitBindingEdits();
        CommitProjectGrid();
        string initialDirectory = Directory.Exists(Configuration.RecipeDirectory)
            ? Configuration.RecipeDirectory
            : Path.GetDirectoryName(_boundProject.RecipeFilePath ?? string.Empty) ?? string.Empty;
        using var dialog = new OpenFileDialog
        {
            Title = $"为“{_boundProject.Name}”选择 MRTEST 配方",
            Filter = "MRTEST 配方 (*.elems)|*.elems|所有文件 (*.*)|*.*",
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        string oldPath = ResolveRecipeFilePath(_boundProject.RecipeFilePath);
        _boundProject.RecipeFilePath = NormalizeFullPath(dialog.FileName);
        if (!_boundProject.RecipeFilePath.Equals(oldPath, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(_boundProject.RecipeName))
        {
            ApplyRecipeNameFromFile(_boundProject, _boundProject.RecipeFilePath);
        }
        UpdateProjectGridRow(_boundProject);
        ShowSelectedBindings();
    }

    private void ButtonChooseStepImage_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime || GetSelectedStep() is not { } step) return;

        CommitBindingEdits();
        CommitStepGrid(_boundProject);
        string initialDirectory = Directory.Exists(Configuration.ImageDirectory)
            ? Configuration.ImageDirectory
            : Path.GetDirectoryName(ResolveImagePath(step.ImagePath)) ?? string.Empty;
        using var dialog = new OpenFileDialog
        {
            Title = $"为“{step.Name}”选择图卡",
            Filter = "图卡 (*.png;*.bmp;*.jpg;*.jpeg;*.tif;*.tiff)|*.png;*.bmp;*.jpg;*.jpeg;*.tif;*.tiff|所有文件 (*.*)|*.*",
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        step.ImagePath = NormalizeFullPath(dialog.FileName);
        UpdateStepGridRow(step);
        ShowSelectedBindings();
    }

    private void SetBindingLabels(TestProject? project, TestStep? step)
    {
        if (labelBindingProject is not null)
        {
            labelBindingProject.Text = project is null
                ? "当前项目：未选择"
                : $"当前项目：{project.Order}. {project.Name}";
        }

        if (labelBindingStep is not null)
        {
            labelBindingStep.Text = step is null
                ? "当前图卡：未选择"
                : $"当前图卡：{step.Order}. {step.DisplayName}";
        }

        if (labelRecipeFile is not null)
        {
            string path = ResolveRecipeFilePath(project?.RecipeFilePath);
            labelRecipeFile.Text = path.Length == 0 ? "配方文件：未绑定" : $"配方文件：{path}";
        }

        if (labelStepImage is not null)
        {
            string path = ResolveImagePath(step?.ImagePath);
            labelStepImage.Text = path.Length == 0 ? "图卡：未绑定" : $"图卡：{path}";
        }

        if (labelImagePreviewPath is not null)
        {
            string path = ResolveImagePath(step?.ImagePath);
            labelImagePreviewPath.Text = path.Length == 0
                ? "预览：未选择图卡"
                : File.Exists(path) ? $"预览：{path}" : $"预览：文件不存在（{path}）";
        }
    }

    private void ShowImagePreview(string? imagePath)
    {
        if (pictureBoxImagePreview is null) return;
        string path = ResolveImagePath(imagePath);
        if (path.Length == 0 || !File.Exists(path))
        {
            ClearImagePreview();
            return;
        }

        try
        {
            // 从可共享文件流读取并立即复制 Bitmap，避免 PictureBox 长期锁住图卡文件。
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var usingImage = Image.FromStream(stream);
            var preview = new Bitmap(usingImage);
            Image? previous = pictureBoxImagePreview.Image;
            pictureBoxImagePreview.Image = preview;
            previous?.Dispose();
        }
        catch (Exception ex)
        {
            ClearImagePreview();
            AppendBindingLog($"图卡预览失败：{ex.Message}");
        }
    }

    private void ClearImagePreview()
    {
        if (pictureBoxImagePreview is null) return;
        Image? previous = pictureBoxImagePreview.Image;
        pictureBoxImagePreview.Image = null;
        previous?.Dispose();
    }

    private TestProject? GetSelectedProject()
    {
        if (gridProjects is null) return null;
        if (gridProjects.CurrentRow?.DataBoundItem is TestProject current)
            return current;

        // During a DataSource transition CurrentRow can briefly be null while
        // SelectedRows still contains the valid bound item.  Prefer the object
        // itself over a display index in that case.
        foreach (DataGridViewRow row in gridProjects.SelectedRows)
        {
            if (row.DataBoundItem is TestProject project)
                return project;
        }
        return null;
    }

    private TestStep? GetSelectedStep()
    {
        if (gridSteps is null) return null;
        if (gridSteps.CurrentRow?.DataBoundItem is TestStep current)
            return current;
        foreach (DataGridViewRow row in gridSteps.SelectedRows)
        {
            if (row.DataBoundItem is TestStep step)
                return step;
        }
        return null;
    }

    private void UpdateProjectGridRow(TestProject project)
    {
        if (gridProjects is null) return;
        foreach (DataGridViewRow row in gridProjects.Rows)
        {
            if (!ReferenceEquals(row.DataBoundItem, project)) continue;
            if (row.IsNewRow) continue;
            if (gridProjects.Columns.Contains("RecipeName") && row.Cells.Count > 0)
                row.Cells["RecipeName"].Value = project.RecipeName;
            if (gridProjects.Columns.Contains("RecipeFilePath"))
                row.Cells["RecipeFilePath"].Value = project.RecipeFilePath;
            if (row.Index >= 0) gridProjects.InvalidateRow(row.Index);
            break;
        }
    }

    private void UpdateStepGridRow(TestStep step)
    {
        if (gridSteps is null) return;
        foreach (DataGridViewRow row in gridSteps.Rows)
        {
            if (!ReferenceEquals(row.DataBoundItem, step)) continue;
            if (row.IsNewRow) continue;
            if (gridSteps.Columns.Contains("ImagePath"))
                row.Cells["ImagePath"].Value = step.ImagePath;
            if (row.Index >= 0) gridSteps.InvalidateRow(row.Index);
            break;
        }
    }

    private static List<string> DiscoverImageFiles(string? root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return new List<string>();

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => SupportedImageExtensions.Contains(Path.GetExtension(path)))
            .Select(NormalizeFullPath)
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string ResolveImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try
        {
            string trimmed = path.Trim();
            return Path.IsPathRooted(trimmed)
                ? NormalizeFullPath(trimmed)
                : NormalizeFullPath(Path.Combine(Configuration.ImageDirectory ?? string.Empty, trimmed));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim();
        }
    }

    private string ResolveRecipeFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try
        {
            string trimmed = path.Trim();
            return Path.IsPathRooted(trimmed)
                ? NormalizeFullPath(trimmed)
                : NormalizeFullPath(Path.Combine(Configuration.RecipeDirectory ?? string.Empty, trimmed));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim();
        }
    }

    private static string NormalizeFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try { return Path.GetFullPath(path.Trim()); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim();
        }
    }

    /// <summary>
    /// 从已绑定的配方文件路径推导 MRTEST 协议中的配方名。
    /// RecipeCatalog 使用同样的文件名（不含扩展名）语义；这里不要求
    /// 文件当前存在，因此用户可以先录入路径、稍后再补齐文件。
    /// </summary>
    private static void ApplyRecipeNameFromFile(
        TestProject project,
        string? recipeFilePath,
        bool onlyWhenEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (onlyWhenEmpty && !string.IsNullOrWhiteSpace(project.RecipeName))
            return;

        string path = recipeFilePath?.Trim() ?? string.Empty;
        if (path.Length == 0)
            return;

        string stem;
        try
        {
            stem = Path.GetFileNameWithoutExtension(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return;
        }

        if (stem.Length > 0)
            project.RecipeName = stem;
    }

    private static string GetImageDisplayName(string path)
    {
        string file = Path.GetFileName(path);
        string directory = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
        return directory.Length == 0 ? file : $"{directory}\\{file}";
    }

    private static void ReindexProjects(IList<TestProject> projects)
    {
        for (int index = 0; index < projects.Count; index++)
        {
            TestProject project = projects[index];
            project.Order = index + 1;
            ReindexSteps(project.Steps);
        }
    }

    private static void ReindexSteps(IList<TestStep>? steps)
    {
        if (steps is null) return;
        for (int index = 0; index < steps.Count; index++)
            steps[index].Order = index + 1;
    }

    private void AppendBindingLog(string message)
    {
        if (_isDesignTime) return;
        // 绑定面板本身没有新增日志框；保留到提示标签/调试输出，
        // 不让目录权限问题打断用户继续用“选择文件”绑定。
        System.Diagnostics.Debug.WriteLine(message);
    }

    #endregion

    private void ButtonAddProject_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;
        CommitBindingEdits(); CommitStepGrid(_boundProject); CommitProjectGrid();
        int order = Configuration.Projects.Count == 0 ? 1 : Configuration.Projects.Max(p => p.Order) + 1;
        Configuration.Projects.Add(new TestProject { Order = order, Name = "新测试项目", Kind = TestProjectKind.Generic, RepeatCount = 1, Steps = new() });
        BindProjects(Configuration.Projects.Count - 1);
        if (gridProjects.Rows.Count > 0 && gridProjects.Columns.Count > 2)
            gridProjects.CurrentCell = gridProjects.Rows[^1].Cells[2];
    }

    private void ButtonDeleteProject_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime || _rebindingGrids) return;

        // Resolve the bound object before committing/resetting the grid.  A
        // DataGridView row index is only a presentation detail and may be
        // transient during SelectionChanged; using the object identity avoids
        // deleting a neighbouring project when rows are sorted or rebound.
        TestProject? project = GetSelectedProject();
        if (project is null || !Configuration.Projects.Contains(project)) return;
        int oldIndex = Configuration.Projects.IndexOf(project);

        CommitBindingEdits();
        CommitStepGrid(_boundProject);
        CommitProjectGrid();

        DialogResult confirmation = MessageBox.Show(
            this,
            $"确定删除测试项目“{project.Name}”吗？\r\n该项目的固定图卡绑定也会一并删除。",
            "删除项目",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes) return;

        Configuration.Projects.Remove(project);
        ReindexProjects(Configuration.Projects);
        _boundProject = null;
        int preferredIndex = Math.Min(Math.Max(oldIndex, 0), Configuration.Projects.Count - 1);
        BindProjects(preferredIndex);
    }

    private void ButtonAddStep_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;
        CommitBindingEdits(); CommitStepGrid(_boundProject); CommitProjectGrid(); TestProject? project = GetSelectedProject();
        if (project is null) return;
        project.Steps.Add(new TestStep { Order = project.Steps.Count + 1, Name = "新图卡", MeasurementRequest = Protocol.MessageProtocol.DefaultMeasurementRequest });
        BindSelectedSteps();
    }

    private void ButtonDeleteStep_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime || _rebindingGrids) return;
        TestProject? project = GetSelectedProject();
        TestStep? step = GetSelectedStep();
        if (project is null || step is null || !project.Steps.Contains(step)) return;

        CommitBindingEdits();
        CommitStepGrid(project);
        CommitProjectGrid();
        if (!project.Steps.Remove(step)) return;
        ReindexSteps(project.Steps);
        BindSelectedSteps();
    }

    private void ButtonSave_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;
        try
        {
            CommitBindingEdits(); CommitProjectGrid(); CommitStepGrid(_boundProject); Configuration.Normalize(); _store.Save(Configuration); DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ButtonCancel_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;
        _cancelRequested = true; DialogResult = DialogResult.Cancel; Close();
    }

    private void RecipeManagerForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_cancelRequested || DialogResult == DialogResult.Cancel) return;
        try
        {
            CommitBindingEdits(); CommitProjectGrid(); CommitStepGrid(_boundProject); Configuration.Normalize(); _store.Save(Configuration); DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            e.Cancel = true;
            MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CommitProjectGrid()
    {
        if (gridProjects.DataSource is not List<TestProject> list) return;
        try { gridProjects.EndEdit(); } catch { }
        // DataGridView 绑定到 List<T> 不会自动写回所有文本单元格；显式读取，保证退出值完整保留。
        // Always resolve the object from DataBoundItem instead of assuming
        // Rows[row] == list[row].  During a rebind (and after deleting a row)
        // that positional assumption is false and used to produce index
        // exceptions or write one project's values into another.
        foreach (DataGridViewRow r in gridProjects.Rows)
        {
            if (r.IsNewRow || r.DataBoundItem is not TestProject p || !list.Contains(p))
                continue;
            string previousRecipePath = p.RecipeFilePath ?? string.Empty;
            string editedRecipePath = CellText(r, "RecipeFilePath", previousRecipePath).Trim();
            p.Enabled = CellBool(r, "Enabled", p.Enabled); p.Order = CellInt(r, "Order", p.Order); p.Name = CellText(r, "Name", p.Name);
            p.RecipeName = CellText(r, "RecipeName", p.RecipeName).Trim();
            p.RecipeFilePath = editedRecipePath;
            // EndEdit normally invokes GridProjects_CellEndEdit first.  This
            // second guard also covers programmatic edits and unusual designer
            // hosts that do not raise the event; a changed path is a new
            // binding and therefore receives the new file-name default.
            if (editedRecipePath.Length > 0 &&
                (!editedRecipePath.Equals(previousRecipePath.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 string.IsNullOrWhiteSpace(p.RecipeName)))
            {
                ApplyRecipeNameFromFile(p, editedRecipePath);
                r.Cells["RecipeName"].Value = p.RecipeName;
            }
            p.RepeatCount = Math.Max(1, CellInt(r, "RepeatCount", p.RepeatCount)); p.PopupDriven = CellBool(r, "PopupDriven", p.PopupDriven); p.CrosstalkStartIndex = Math.Max(0, CellInt(r, "CrosstalkStartIndex", p.CrosstalkStartIndex)); p.PopupTimeoutSeconds = Math.Clamp(CellInt(r, "PopupTimeoutSeconds", p.PopupTimeoutSeconds), 1, 600);
            object? kind = r.Cells["Kind"].Value; if (kind is TestProjectKind k) p.Kind = k; else if (kind is string s && Enum.TryParse(s, out TestProjectKind parsed)) p.Kind = parsed;
        }
    }

    private void CommitStepGrid(TestProject? project)
    {
        if (project is null) return;
        try { gridSteps.EndEdit(); } catch { }
        var list = project.Steps;
        // As with projects, use DataBoundItem rather than a row index.  The
        // step grid is rebound whenever the project selection changes, and a
        // transient old row must never be interpreted as the new project's
        // step at the same index.
        foreach (DataGridViewRow r in gridSteps.Rows)
        {
            if (r.IsNewRow || r.DataBoundItem is not TestStep s || !list.Contains(s))
                continue;
            s.Order = CellInt(r, "StepOrder", s.Order);
            s.Name = CellText(r, "StepName", s.Name);
            s.ImagePath = CellText(r, "ImagePath", s.ImagePath);
            s.StabilizeDelayMs = Math.Max(0, CellInt(r, "Delay", s.StabilizeDelayMs));
            string request = CellText(r, "Request", s.MeasurementRequest);
            s.MeasurementRequest = string.IsNullOrWhiteSpace(request)
                ? Protocol.MessageProtocol.DefaultMeasurementRequest
                : Protocol.MessageProtocol.MigrateMeasurementRequest(request);
            if (gridSteps.Columns.Contains("Request"))
                r.Cells["Request"].Value = s.MeasurementRequest;
        }
    }

    private static string CellText(DataGridViewRow row, string name, string fallback)
    {
        if (row.DataGridView is null || !row.DataGridView.Columns.Contains(name)) return fallback;
        return row.Cells[name].Value?.ToString() ?? fallback;
    }

    private static int CellInt(DataGridViewRow row, string name, int fallback)
    {
        if (row.DataGridView is null || !row.DataGridView.Columns.Contains(name)) return fallback;
        return int.TryParse(row.Cells[name].Value?.ToString(), out int value) ? value : fallback;
    }

    private static bool CellBool(DataGridViewRow row, string name, bool fallback)
    {
        if (row.DataGridView is null || !row.DataGridView.Columns.Contains(name)) return fallback;
        return row.Cells[name].Value is bool b
            ? b
            : bool.TryParse(row.Cells[name].Value?.ToString(), out bool value) ? value : fallback;
    }

    private static TestPlanConfiguration Clone(TestPlanConfiguration value)
        => value.Clone();
}
