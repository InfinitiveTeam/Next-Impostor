using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net
{
    /// <summary>
    /// 管理 DTLS 监听器所需的 RSA 自签名证书。
    /// 证书会持久化到磁盘，避免每次启动重新生成导致客户端验证失败。
    /// </summary>
    internal class DtlsCertificateService
    {
        private static readonly ILogger Logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        private const string CertFile = "dtls_cert.pem";
        private const string KeyFile = "dtls_key.pem";

        private X509Certificate2? _certificate;

        public X509Certificate2 GetOrCreateCertificate()
        {
            if (_certificate != null)
            {
                return _certificate;
            }

            // 尝试从磁盘加载已有证书
            if (File.Exists(CertFile) && File.Exists(KeyFile))
            {
                try
                {
                    _certificate = LoadFromDisk();
                    if (_certificate != null && _certificate.NotAfter > DateTime.UtcNow.AddDays(7))
                    {
                        return _certificate;
                    }
                }
                catch
                {
                    // 加载失败则重新生成
                }
            }

            // 生成新的自签名 RSA 证书（2048-bit，2年有效期）
            _certificate = GenerateSelfSigned();
            SaveToDisk(_certificate);
            return _certificate;
        }

        private static X509Certificate2 GenerateSelfSigned()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=Impostor DTLS",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));

            var cert = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(2));

            // 确保包含私钥
            return new X509Certificate2(cert.Export(X509ContentType.Pfx));
        }

        private static X509Certificate2? LoadFromDisk()
        {
            var certPem = File.ReadAllText(CertFile);
            var keyPem = File.ReadAllText(KeyFile);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(keyPem);
            var cert = X509Certificate2.CreateFromPem(certPem);
            return cert.CopyWithPrivateKey(rsa);
        }

        private static void SaveToDisk(X509Certificate2 cert)
        {
            try
            {
                File.WriteAllText(CertFile, cert.ExportCertificatePem());
                using var rsa = cert.GetRSAPrivateKey()!;
                File.WriteAllText(KeyFile, rsa.ExportRSAPrivateKeyPem());
            }
            catch
            {
                // 保存失败不影响运行
            }
        }
    }
}
