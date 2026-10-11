using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Sciuscia.Foundations;

namespace Sciuscia.Ui;

/// <summary>
/// Finestra unica dell'applicazione: contiene solo un controllo WebView2 che mostra le
/// schermate (pagine in wwwroot). Il ponte C#↔JavaScript (<see cref="Bridge"/>) è l'unico punto
/// di contatto tra le pagine e i servizi già collaudati di Fondazioni/Anagrafiche/Commesse/
/// Operazioni: le pagine non parlano mai direttamente con il database.
/// </summary>
public sealed class MainForm : Form
{
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private Bridge? _bridge;

    public MainForm()
    {
        Text = "Gestionale Sciuscià";
        Width = 1280;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_webView);
        Load += OnFormLoad;
    }

    private async void OnFormLoad(object? sender, EventArgs e)
    {
        try
        {
            var runtime = new FoundationRuntime(new FoundationOptions(AppPaths.DefaultDataRoot()));
            var installation = await runtime.Database.EnsureCreatedAsync();
            _bridge = new Bridge(runtime, installation);

            await _webView.EnsureCoreWebView2Async();

            var webRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "sciuscia.app", webRootPath, CoreWebView2HostResourceAccessKind.Allow);
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _webView.CoreWebView2.Navigate("https://sciuscia.app/index.html");
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"Avvio non riuscito. Dettaglio: {exception.Message}",
                "Gestionale Sciuscià",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_bridge is null)
        {
            return;
        }

        var requestJson = e.TryGetWebMessageAsString();
        var responseJson = await _bridge.HandleAsync(requestJson);

        if (_webView.CoreWebView2 is not null)
        {
            _webView.CoreWebView2.PostWebMessageAsString(responseJson);
        }
    }
}
