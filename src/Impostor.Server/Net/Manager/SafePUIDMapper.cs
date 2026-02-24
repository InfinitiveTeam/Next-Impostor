using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Impostor.Server.Net.Manager
{
    /// <summary>
    /// 安全的 PUID 映射管理器
    /// 确保每个 PUID 只能有一个活跃连接
    /// 防止在 NAT 环境中多个玩家使用相同 PUID 导致的问题
    /// </summary>
    public class SafePUIDMapper
    {
        // ClientId -> PUID 映射
        private readonly ConcurrentDictionary<int, string> _clientIdToPuid;
        
        // PUID -> ClientId 反向映射（快速查找）
        private readonly ConcurrentDictionary<string, int> _puidToClientId;

        public SafePUIDMapper()
        {
            _clientIdToPuid = new();
            _puidToClientId = new();
        }

        /// <summary>
        /// 尝试为客户端注册 PUID
        /// 如果 PUID 已被其他客户端使用，返回 false
        /// </summary>
        public bool TryRegisterPUID(int clientId, string puid)
        {
            if (string.IsNullOrEmpty(puid))
            {
                return false;
            }

            // 检查 PUID 是否已被占用
            if (_puidToClientId.ContainsKey(puid))
            {
                return false;  // PUID 已在线
            }

            // 添加正向映射
            if (!_clientIdToPuid.TryAdd(clientId, puid))
            {
                return false;
            }

            // 添加反向映射
            if (!_puidToClientId.TryAdd(puid, clientId))
            {
                // 反向映射失败，回滚正向映射
                _clientIdToPuid.TryRemove(clientId, out _);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 注销客户端的 PUID（客户端断开时调用）
        /// </summary>
        public bool TryUnregisterPUID(int clientId)
        {
            if (_clientIdToPuid.TryRemove(clientId, out var puid))
            {
                _puidToClientId.TryRemove(puid, out _);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取客户端的 PUID
        /// </summary>
        public bool TryGetPUID(int clientId, out string puid)
        {
            return _clientIdToPuid.TryGetValue(clientId, out puid);
        }

        /// <summary>
        /// 检查 PUID 是否已在线
        /// </summary>
        public bool IsPUIDOnline(string puid)
        {
            return _puidToClientId.ContainsKey(puid);
        }

        /// <summary>
        /// 获取 PUID 对应的客户端 ID
        /// </summary>
        public bool TryGetClientIdByPUID(string puid, out int clientId)
        {
            return _puidToClientId.TryGetValue(puid, out clientId);
        }

        /// <summary>
        /// 获取所有已注册的 PUID
        /// </summary>
        public IEnumerable<string> GetAllRegisteredPUIDs()
        {
            return _puidToClientId.Keys;
        }

        /// <summary>
        /// 清除所有映射（仅用于测试或关闭）
        /// </summary>
        public void Clear()
        {
            _clientIdToPuid.Clear();
            _puidToClientId.Clear();
        }
    }
}
