namespace NexusPoint.Utils
{
    public static class PasswordHasher
    {
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                System.Diagnostics.Debug.WriteLine("Password hash verification failed: Invalid salt format.");
                return false;
            }
            catch (System.ArgumentException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Password hash verification error: {ex.Message}");
                return false;
            }
        }
    }
}