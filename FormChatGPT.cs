﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChatGPTTest
{
    public partial class FormChatGPT : Form
    {
        private bool m_bFirstTime = true;
        private const string RegistryPath = "Software\\KTS InfoTech\\ChatGptTest";
        private const string OpenAI_ApiUrl = "https://api.openai.com/v1/chat/completions";
        private const string OpenAI_ApiModel = "gpt-4o-mini";
        public ApiSettings m_OpenApiSettings ;
        private List<object> conversationHistory = new List<object>();

        public FormChatGPT()
        {
            InitializeComponent();
            InitializeWebView();
            m_OpenApiSettings = LoadApiSettingsFromRegistry();  

        }
        
        private async void InitializeWebView()
        {
            await webViewGPT.EnsureCoreWebView2Async();
            LoadOpeningImage();

        }
        private void SetStatusMessage(string message)
        {
            toolStripStatusLabel1.Text = message;
        }

        public ApiSettings LoadApiSettingsFromRegistry()
        {
            ApiSettings settings = new ApiSettings("ChatGPT | OpenAPI", OpenAI_ApiUrl, OpenAI_ApiModel, "");
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        settings.ApiLLMName = key.GetValue("ApiLLMName") as string;
                        settings.ApiKey = key.GetValue("ApiKey") as string;
                        settings.ApiUrl = key.GetValue("ApiUrl") as string;
                        settings.AiModel = key.GetValue("AiModel") as string;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading API settings: {ex.Message}");
            }
            return settings;
        }
        public void SaveApiSettingsToRegistry(ApiSettings settings)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        key.SetValue("ApiLLMName", settings.ApiLLMName ?? "");
                        key.SetValue("ApiKey", settings.ApiKey ?? "");
                        key.SetValue("ApiUrl", settings.ApiUrl ?? "");
                        key.SetValue("AiModel", settings.AiModel ?? "");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving API settings: {ex.Message}");
            }
        }

        private async Task<string> GetChatGPTResponse(string message)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {m_OpenApiSettings.ApiKey}");

                // Append the new message to history
                conversationHistory.Add(new { role = "user", content = message });

                var request = new
                {
                    model = m_OpenApiSettings.AiModel,
                    messages = conversationHistory
                };

                var response = await client.PostAsJsonAsync(m_OpenApiSettings.ApiUrl, request);
                var result = await response.Content.ReadAsStringAsync();

                var jsonDoc = JsonDocument.Parse(result);
                var content = jsonDoc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (!string.IsNullOrEmpty(content))
                {
                    // Append the AI response to history
                    conversationHistory.Add(new { role = "assistant", content = content });
                }

                return content ?? "Error: No response";
            }
        }

        private void buttonProperties_Click(object sender, EventArgs e)
        {
            FormLLMSettings Settings = new FormLLMSettings(m_OpenApiSettings);
            if (Settings.ShowDialog() == DialogResult.OK)
            {
                m_OpenApiSettings = Settings.GetApiSettings();
                SaveApiSettingsToRegistry(m_OpenApiSettings);
            }
        }

        private void newChatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NewChat();
        }
        private void NewChat()
        {
            webViewGPT.ExecuteScriptAsync("document.body.innerHTML = '';");
            webViewGPT.NavigateToString("<html><body></body></html>");
            conversationHistory.Clear();
            
        }
        private async void buttonAsk_Click_1(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(m_OpenApiSettings.ApiKey))
                {
                    MessageBox.Show("Please set the API Key", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (m_bFirstTime)
                {
                    NewChat();
                    m_bFirstTime = false;
                }
                SetStatusMessage("Please Wait..");

                string userMessage = textBoxQuery.Text.Trim();
                if (string.IsNullOrEmpty(userMessage)) return;

                string chatResponse = await GetChatGPTResponse(userMessage);
                chatResponse = Regex.Replace(chatResponse, @"\*\*(.*?)\*\*", "<b>$1</b>");
                chatResponse = Regex.Replace(chatResponse, @"### (.*)", "<h4>$1</h4>");

                // Wrap message in a div with a unique class
                string htmlResponse = $@"
            <div class='chat-message'>
                <h3>User:</h3>
                <p>{userMessage}</p>
                <h3>ChatGPT:</h3>
                <p>{chatResponse.Replace("\n", "<br>")}</p>
            </div>";

                // Append the new content
                await webViewGPT.ExecuteScriptAsync($@"document.body.innerHTML += `{htmlResponse}`");

                // Scroll to the last chat message
                await webViewGPT.ExecuteScriptAsync(@"
            setTimeout(() => {
                let messages = document.getElementsByClassName('chat-message');
                if (messages.length > 0) {
                    messages[messages.length - 1].scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            }, 100);"); // Delay added to ensure DOM updates

                SetStatusMessage("Done..");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        private void buttonNew_Click(object sender, EventArgs e)
        {
            NewChat();
            LoadOpeningImage();
            textBoxQuery.Text = "";
        }

        private void LoadOpeningImage()
        {
            string base64Image = ConvertBitmapToBase64(ConvertResourceImageToBitmap(Properties.Resources.AI_Chat_Browser));
            string htmlContent = $@"
        <html>
        <head>
            <style>
                body {{
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    height: 100vh;
                    margin: 0;
                    background-color: #f5f5f5;
                }}
                img {{
                    max-width: 80%;
                    max-height: 80%;
                }}
            </style>
        </head>
        <body>
            <img src='data:image/jpeg;base64,{base64Image}' alt='Opening Image'>
        </body>
        </html>";

            webViewGPT.NavigateToString(htmlContent);
            m_bFirstTime = true;
        }

        private Bitmap ConvertResourceImageToBitmap(byte[] imageBytes)
        {
            using (MemoryStream ms = new MemoryStream(imageBytes))
            {
                return new Bitmap(ms);
            }
        }

        private string ConvertBitmapToBase64(Bitmap image)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    using (Bitmap clone = new Bitmap(image))  // Clone the bitmap to avoid GDI+ issues
                    {
                        clone.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error converting image: " + ex.Message);
                return string.Empty;
            }
        }
        
    }
}
       

