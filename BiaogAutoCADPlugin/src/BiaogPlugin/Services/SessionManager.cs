using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using BiaogPlugin.Models;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// AI助手会话管理器
    /// 负责会话的创建、加载、保存、删除等操作
    /// </summary>
    public class SessionManager
    {
        private readonly string _sessionsDirectory;
        private readonly List<ChatSession> _sessions = new();
        private ChatSession? _currentSession;

        /// <summary>
        /// 当前激活的会话
        /// </summary>
        public ChatSession? CurrentSession => _currentSession;

        /// <summary>
        /// 所有会话列表
        /// </summary>
        public IReadOnlyList<ChatSession> Sessions => _sessions.AsReadOnly();

        /// <summary>
        /// 会话切换事件
        /// </summary>
        public event EventHandler<ChatSession>? SessionChanged;

        /// <summary>
        /// 会话列表更新事件
        /// </summary>
        public event EventHandler? SessionsUpdated;

        public SessionManager()
        {
            // 会话保存目录：%USERPROFILE%\.biaoge\sessions\
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _sessionsDirectory = Path.Combine(userProfile, ".biaoge", "sessions");

            // 确保目录存在
            if (!Directory.Exists(_sessionsDirectory))
            {
                Directory.CreateDirectory(_sessionsDirectory);
                Log.Information($"创建会话目录: {_sessionsDirectory}");
            }

            // 加载所有会话
            LoadAllSessions();

            // 如果没有会话，创建第一个
            if (_sessions.Count == 0)
            {
                CreateNewSession();
            }
            else
            {
                // 加载最近的会话
                _currentSession = _sessions.OrderByDescending(s => s.LastUpdateTime).First();
            }
        }

        /// <summary>
        /// 创建新会话
        /// </summary>
        public ChatSession CreateNewSession(string? title = null)
        {
            var session = new ChatSession
            {
                Title = title ?? "新对话",
                CreateTime = DateTime.Now,
                LastUpdateTime = DateTime.Now
            };

            _sessions.Add(session);
            _currentSession = session;

            SaveSession(session);
            SessionChanged?.Invoke(this, session);
            SessionsUpdated?.Invoke(this, EventArgs.Empty);

            Log.Information($"创建新会话: {session.Id}");
            return session;
        }

        /// <summary>
        /// 切换到指定会话
        /// </summary>
        public void SwitchToSession(string sessionId)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session == null)
            {
                Log.Warning($"会话不存在: {sessionId}");
                return;
            }

            _currentSession = session;
            SessionChanged?.Invoke(this, session);
            Log.Information($"切换到会话: {sessionId}");
        }

        /// <summary>
        /// 删除会话
        /// </summary>
        public void DeleteSession(string sessionId)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session == null)
            {
                Log.Warning($"会话不存在: {sessionId}");
                return;
            }

            // 删除文件
            var filePath = GetSessionFilePath(sessionId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Log.Information($"删除会话文件: {filePath}");
            }

            // 从列表移除
            _sessions.Remove(session);

            // 如果删除的是当前会话，切换到另一个
            if (_currentSession?.Id == sessionId)
            {
                if (_sessions.Count > 0)
                {
                    _currentSession = _sessions.OrderByDescending(s => s.LastUpdateTime).First();
                    SessionChanged?.Invoke(this, _currentSession);
                }
                else
                {
                    // 没有会话了，创建新的
                    CreateNewSession();
                }
            }

            SessionsUpdated?.Invoke(this, EventArgs.Empty);
            Log.Information($"删除会话: {sessionId}");
        }

        /// <summary>
        /// 保存当前会话
        /// </summary>
        public void SaveCurrentSession()
        {
            if (_currentSession != null)
            {
                SaveSession(_currentSession);
            }
        }

        /// <summary>
        /// 保存指定会话到磁盘
        /// </summary>
        private void SaveSession(ChatSession session)
        {
            try
            {
                session.LastUpdateTime = DateTime.Now;

                var filePath = GetSessionFilePath(session.Id);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(session, options);
                File.WriteAllText(filePath, json);

                Log.Debug($"保存会话: {session.Id}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"保存会话失败: {session.Id}");
            }
        }

        /// <summary>
        /// 加载所有会话
        /// </summary>
        private void LoadAllSessions()
        {
            try
            {
                var files = Directory.GetFiles(_sessionsDirectory, "*.json");
                Log.Information($"找到 {files.Length} 个会话文件");

                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var session = JsonSerializer.Deserialize<ChatSession>(json);
                        if (session != null)
                        {
                            _sessions.Add(session);
                            Log.Debug($"加载会话: {session.Id} - {session.Title}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, $"加载会话文件失败: {file}");
                    }
                }

                // 按最后更新时间排序
                _sessions.Sort((a, b) => b.LastUpdateTime.CompareTo(a.LastUpdateTime));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载会话列表失败");
            }
        }

        /// <summary>
        /// 获取会话文件路径
        /// </summary>
        private string GetSessionFilePath(string sessionId)
        {
            return Path.Combine(_sessionsDirectory, $"{sessionId}.json");
        }

        /// <summary>
        /// 更新当前会话的消息历史
        /// </summary>
        public void UpdateCurrentSessionMessages(List<ChatMessage> messages)
        {
            if (_currentSession != null)
            {
                _currentSession.Messages = messages;
                _currentSession.LastUpdateTime = DateTime.Now;

                // 如果标题还是"新对话"，自动生成标题
                if (_currentSession.Title == "新对话" && messages.Count > 0)
                {
                    _currentSession.AutoGenerateTitle();
                    SessionsUpdated?.Invoke(this, EventArgs.Empty);
                }

                SaveSession(_currentSession);
            }
        }

        /// <summary>
        /// 重命名会话
        /// </summary>
        public void RenameSession(string sessionId, string newTitle)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                session.Title = newTitle;
                SaveSession(session);
                SessionsUpdated?.Invoke(this, EventArgs.Empty);
                Log.Information($"重命名会话: {sessionId} -> {newTitle}");
            }
        }

        /// <summary>
        /// 清空所有会话
        /// </summary>
        public void ClearAllSessions()
        {
            foreach (var session in _sessions.ToList())
            {
                DeleteSession(session.Id);
            }
        }
    }
}
