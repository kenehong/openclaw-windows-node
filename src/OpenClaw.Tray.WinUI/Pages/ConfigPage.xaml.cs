using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenClawTray.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenClawTray.Pages;

public sealed partial class ConfigPage : Page
{
    private HubWindow? _hub;
    private JsonElement? _lastConfig;
    private JsonElement? _selectedElement;
    private string _selectedPath = "";
    private readonly Dictionary<TreeViewNode, (string Path, JsonElement Element)> _nodeMap = new();

    public ConfigPage()
    {
        InitializeComponent();
    }

    public void Initialize(HubWindow hub)
    {
        _hub = hub;
        if (hub.GatewayClient != null)
        {
            ConnectionWarning.Visibility = Visibility.Collapsed;
            StatusText.Text = "Requesting configuration...";
            _ = hub.GatewayClient.RequestConfigAsync();
        }
        else
        {
            ConnectionWarning.Visibility = Visibility.Visible;
            StatusText.Text = "Not connected";
        }
    }

    public void UpdateConfig(JsonElement config)
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            _lastConfig = config;
            _nodeMap.Clear();
            ConfigTree.RootNodes.Clear();

            var configRoot = config;
            if (config.TryGetProperty("path", out var pathEl))
                ConfigPathText.Text = $"\U0001F4C4 {pathEl.GetString()}";
            if (config.TryGetProperty("config", out var inner))
                configRoot = inner;

            BuildTreeNodes(ConfigTree.RootNodes, configRoot, "");

            foreach (var node in ConfigTree.RootNodes)
                node.IsExpanded = true;

            StatusText.Text = $"Loaded {CountKeys(configRoot)} config keys";
        });
    }

    private void BuildTreeNodes(IList<TreeViewNode> parent, JsonElement element, string basePath)
    {
        if (element.ValueKind != JsonValueKind.Object) return;

        foreach (var prop in element.EnumerateObject())
        {
            var path = string.IsNullOrEmpty(basePath) ? prop.Name : $"{basePath}.{prop.Name}";
            var node = new TreeViewNode();

            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                // Skip shallow objects that only have leaf children — they'll show in parent detail
                bool hasObjectOrArrayChild = false;
                foreach (var child in prop.Value.EnumerateObject())
                {
                    if (child.Value.ValueKind == JsonValueKind.Object || child.Value.ValueKind == JsonValueKind.Array)
                    { hasObjectOrArrayChild = true; break; }
                }

                if (!hasObjectOrArrayChild)
                {
                    // Shallow object (all leaves) — skip tree node, show in parent detail
                    continue;
                }

                node.Content = $"📁 {prop.Name}";
                node.IsExpanded = false;
                _nodeMap[node] = (path, prop.Value);
                BuildTreeNodes(node.Children, prop.Value, path);
            }
            else if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                node.Content = $"📋 {prop.Name} [{prop.Value.GetArrayLength()}]";
                _nodeMap[node] = (path, prop.Value);
                // Only add array item children if items are objects
                int idx = 0;
                foreach (var item in prop.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        var childNode = new TreeViewNode();
                        var itemPath = $"{path}[{idx}]";
                        var label = TryGetLabel(item) ?? $"[{idx}]";
                        childNode.Content = $"  {label}";
                        _nodeMap[childNode] = (itemPath, item);
                        node.Children.Add(childNode);
                    }
                    idx++;
                }
            }
            else
            {
                continue; // Leaf values show in detail panel
            }

            parent.Add(node);
        }
    }

    private void OnTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode node && _nodeMap.TryGetValue(node, out var entry))
        {
            _selectedElement = entry.Element;
            _selectedPath = entry.Path;
            ShowDetail(entry.Path, entry.Element);
        }
    }

    private void ShowDetail(string path, JsonElement element)
    {
        DetailPanel.Children.Clear();
        DetailPlaceholder.Visibility = Visibility.Collapsed;
        DetailPath.Text = path;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                DetailType.Text = $"Object \u00B7 {element.EnumerateObject().Count()} properties";
                foreach (var prop in element.EnumerateObject())
                {
                    var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var keyBlock = new TextBlock
                    {
                        Text = prop.Name,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 13
                    };
                    Grid.SetColumn(keyBlock, 0);
                    row.Children.Add(keyBlock);

                    var propPath = $"{path}.{prop.Name}";
                    var editControl = CreateEditableControl(prop.Value, propPath);
                    Grid.SetColumn(editControl, 1);
                    row.Children.Add(editControl);

                    DetailPanel.Children.Add(row);

                    DetailPanel.Children.Add(new Border
                    {
                        Height = 1,
                        Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                        Margin = new Thickness(0, 2, 0, 2),
                        Opacity = 0.3
                    });
                }
                break;

            case JsonValueKind.Array:
                DetailType.Text = $"Array \u00B7 {element.GetArrayLength()} items";
                int idx = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var card = new Border
                    {
                        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12, 8, 12, 8),
                        Margin = new Thickness(0, 4, 0, 4)
                    };

                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        var sp = new StackPanel { Spacing = 4 };
                        sp.Children.Add(new TextBlock
                        {
                            Text = TryGetLabel(item) ?? $"Item {idx}",
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 13
                        });
                        foreach (var sub in item.EnumerateObject())
                        {
                            var subRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                            subRow.Children.Add(new TextBlock
                            {
                                Text = sub.Name,
                                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                                FontSize = 12, Width = 140
                            });
                            subRow.Children.Add(new TextBlock
                            {
                                Text = FormatValue(sub.Value),
                                FontFamily = new FontFamily("Consolas"),
                                FontSize = 12,
                                IsTextSelectionEnabled = true,
                                TextWrapping = TextWrapping.Wrap
                            });
                            sp.Children.Add(subRow);
                        }
                        card.Child = sp;
                    }
                    else
                    {
                        card.Child = new TextBlock
                        {
                            Text = FormatValue(item),
                            FontFamily = new FontFamily("Consolas"),
                            IsTextSelectionEnabled = true
                        };
                    }
                    DetailPanel.Children.Add(card);
                    idx++;
                }
                break;

            default:
                DetailType.Text = element.ValueKind.ToString();
                var valueText = new TextBlock
                {
                    Text = FormatValue(element),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 16,
                    IsTextSelectionEnabled = true,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                DetailPanel.Children.Add(valueText);
                break;
        }
    }

    private FrameworkElement CreateValueDisplay(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.True:
            case JsonValueKind.False:
                var boolText = value.GetBoolean() ? "true" : "false";
                var boolColor = value.GetBoolean()
                    ? global::Windows.UI.Color.FromArgb(255, 34, 139, 34)
                    : global::Windows.UI.Color.FromArgb(255, 178, 34, 34);
                return new Border
                {
                    Background = new SolidColorBrush(boolColor),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 2, 8, 2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock { Text = boolText, Foreground = new SolidColorBrush(Colors.White), FontSize = 12, FontWeight = FontWeights.SemiBold }
                };

            case JsonValueKind.Number:
                return new TextBlock
                {
                    Text = value.GetRawText(),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 13,
                    Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 86, 156, 214)),
                    IsTextSelectionEnabled = true,
                    VerticalAlignment = VerticalAlignment.Center
                };

            case JsonValueKind.Null:
                return new TextBlock
                {
                    Text = "null",
                    FontStyle = global::Windows.UI.Text.FontStyle.Italic,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                };

            case JsonValueKind.Object:
                var objPanel = new StackPanel { Spacing = 2 };
                foreach (var sub in value.EnumerateObject())
                {
                    var subRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                    subRow.Children.Add(new TextBlock
                    {
                        Text = $"{sub.Name}:",
                        FontSize = 12,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    });
                    subRow.Children.Add(new TextBlock
                    {
                        Text = FormatValue(sub.Value),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12
                    });
                    objPanel.Children.Add(subRow);
                }
                return objPanel;

            case JsonValueKind.Array:
                var length = value.GetArrayLength();
                if (length == 0)
                    return new TextBlock { Text = "[ empty ]", FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };

                bool allStrings = true;
                foreach (var item in value.EnumerateArray())
                    if (item.ValueKind != JsonValueKind.String) { allStrings = false; break; }

                if (allStrings && length <= 30)
                {
                    var tagsText = string.Join(", ", value.EnumerateArray().Select(v => v.GetString()));
                    return new TextBlock
                    {
                        Text = tagsText,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true
                    };
                }

                return new TextBlock
                {
                    Text = $"[ {length} items ]",
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };

            default: // String
                var str = value.GetString() ?? "";
                if (str.Length > 8 && (str.Contains("token") || str.Contains("key") || str.Contains("secret") || str.Contains("password")))
                    str = str[..4] + "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022" + str[^4..];
                return new TextBlock
                {
                    Text = $"\"{str}\"",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 13,
                    Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 206, 145, 120)),
                    IsTextSelectionEnabled = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };
        }
    }

    private static string? TryGetLabel(JsonElement obj)
    {
        foreach (var key in new[] { "name", "id", "displayName", "key", "title" })
        {
            if (obj.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String)
                return val.GetString();
        }
        return null;
    }

    private static string FormatValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => value.GetRawText()
    };

    private static int CountKeys(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return 0;
        int count = 0;
        foreach (var prop in element.EnumerateObject())
        {
            count++;
            if (prop.Value.ValueKind == JsonValueKind.Object)
                count += CountKeys(prop.Value);
        }
        return count;
    }

    private FrameworkElement CreateEditableControl(JsonElement value, string configPath)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var str = value.GetString() ?? "";
                var isSecret = configPath.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                              configPath.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                              configPath.Contains("secret", StringComparison.OrdinalIgnoreCase);
                var textBox = new TextBox
                {
                    Text = str,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 13,
                    MinWidth = 200,
                    Tag = configPath
                };
                if (isSecret && str.Length > 8)
                {
                    textBox.Text = str[..4] + "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022";
                    textBox.IsReadOnly = true;
                    textBox.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
                }
                else
                {
                    textBox.LostFocus += (s, e) => OnValueEdited(configPath, textBox.Text);
                }
                return textBox;

            case JsonValueKind.Number:
                var numBox = new TextBox
                {
                    Text = value.GetRawText(),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 13,
                    MinWidth = 100,
                    Tag = configPath
                };
                numBox.LostFocus += (s, e) =>
                {
                    if (int.TryParse(numBox.Text, out var intVal))
                        OnValueEdited(configPath, intVal);
                    else if (double.TryParse(numBox.Text, out var dblVal))
                        OnValueEdited(configPath, dblVal);
                };
                return numBox;

            case JsonValueKind.True:
            case JsonValueKind.False:
                var toggle = new ToggleSwitch
                {
                    IsOn = value.GetBoolean(),
                    OnContent = "true",
                    OffContent = "false",
                    MinWidth = 0,
                    Tag = configPath
                };
                toggle.Toggled += (s, e) => OnValueEdited(configPath, toggle.IsOn);
                return toggle;

            case JsonValueKind.Null:
                return new TextBlock
                {
                    Text = "null",
                    FontStyle = global::Windows.UI.Text.FontStyle.Italic,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                };

            case JsonValueKind.Object:
                var objPanel = new StackPanel { Spacing = 2 };
                foreach (var sub in value.EnumerateObject())
                {
                    var subRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                    subRow.Children.Add(new TextBlock
                    {
                        Text = $"{sub.Name}:",
                        FontSize = 12,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    });
                    subRow.Children.Add(new TextBlock
                    {
                        Text = FormatValue(sub.Value),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12
                    });
                    objPanel.Children.Add(subRow);
                }
                return objPanel;

            case JsonValueKind.Array:
                var arr = value;
                bool allStr = true;
                foreach (var item in arr.EnumerateArray())
                    if (item.ValueKind != JsonValueKind.String) { allStr = false; break; }

                if (allStr && arr.GetArrayLength() <= 30)
                {
                    return new TextBlock
                    {
                        Text = string.Join(", ", arr.EnumerateArray().Select(v => v.GetString())),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true
                    };
                }
                return new TextBlock
                {
                    Text = $"[ {arr.GetArrayLength()} items ]",
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    FontSize = 12
                };

            default:
                return new TextBlock { Text = value.GetRawText(), FontFamily = new FontFamily("Consolas"), FontSize = 13 };
        }
    }

    private void OnValueEdited(string configPath, object newValue)
    {
        if (_hub?.GatewayClient == null) return;

        // NOTE: config.set contract may differ from gateway expectations.
        // This sends { path, value } — gateway may expect full config + baseHash.
        // Edits are best-effort; refresh after to verify.
        StatusText.Text = $"Saving {configPath}... (experimental)";
        _ = Task.Run(async () =>
        {
            try
            {
                var success = await _hub.GatewayClient.SetConfigAsync(configPath, newValue);
                DispatcherQueue?.TryEnqueue(() =>
                {
                    StatusText.Text = success 
                        ? $"✅ Sent {configPath} — refresh to verify" 
                        : $"❌ Failed to save {configPath}";
                    // Auto-refresh after save attempt
                    if (success && _hub?.GatewayClient != null)
                        _ = _hub.GatewayClient.RequestConfigAsync();
                });
            }
            catch (Exception ex)
            {
                DispatcherQueue?.TryEnqueue(() => StatusText.Text = $"❌ {ex.Message}");
            }
        });
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        if (_hub?.GatewayClient != null)
        {
            StatusText.Text = "Refreshing...";
            _ = _hub.GatewayClient.RequestConfigAsync();
        }
    }
}
