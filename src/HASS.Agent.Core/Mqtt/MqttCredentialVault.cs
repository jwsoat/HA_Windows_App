using Windows.Security.Credentials;
using Serilog;

namespace HASS.Agent.Core.Mqtt
{
    /// <summary>
    /// Stores MQTT credentials in Windows Credential Manager instead of plain-text JSON.
    /// </summary>
    public class MqttCredentialVault
    {
        private const string Resource = "HASS.Agent.MQTT";

        public void Store(string username, string password)
        {
            try
            {
                var vault = new PasswordVault();
                // remove existing first to avoid duplicates
                try
                {
                    var old = vault.Retrieve(Resource, username);
                    vault.Remove(old);
                }
                catch { /* not found — fine */ }

                vault.Add(new PasswordCredential(Resource, username, password));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MQTT-VAULT] Error storing credentials: {err}", ex.Message);
            }
        }

        public (string Username, string Password) Retrieve(string username)
        {
            try
            {
                var vault = new PasswordVault();
                var cred = vault.Retrieve(Resource, username);
                cred.RetrievePassword();
                return (cred.UserName, cred.Password);
            }
            catch
            {
                return (username, string.Empty);
            }
        }

        public bool HasCredentials(string username)
        {
            try
            {
                var vault = new PasswordVault();
                vault.Retrieve(Resource, username);
                return true;
            }
            catch { return false; }
        }

        public void Remove(string username)
        {
            try
            {
                var vault = new PasswordVault();
                var cred = vault.Retrieve(Resource, username);
                vault.Remove(cred);
            }
            catch { }
        }

        /// <summary>
        /// Migrates a plaintext password from AppSettings into the vault, then clears it from settings.
        /// Call this on first launch of the new app version.
        /// </summary>
        public void MigrateFromPlaintext(string username, string plaintextPassword)
        {
            if (string.IsNullOrWhiteSpace(plaintextPassword)) return;
            if (HasCredentials(username)) return;

            Log.Information("[MQTT-VAULT] Migrating MQTT password to credential vault");
            Store(username, plaintextPassword);
        }
    }
}
