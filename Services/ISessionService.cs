using MeBio.Models;

namespace MeBio.Services;

public interface ISessionService
{
    User? CurrentUser { get; }
    bool IsLoggedIn { get; }
    void SetUser(User user);
    void Clear();
}

public class SessionService : ISessionService
{
    public User? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser is not null;

    public void SetUser(User user) => CurrentUser = user;
    public void Clear() => CurrentUser = null;
}
