namespace Sandbox.Apple;

// UIKit requires the scene lifecycle (Info.plist UIApplicationSceneManifest): the window is created by SceneDelegate.
[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions) => true;
}

[Register("SceneDelegate")]
public sealed class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    [Export("window")]
    public UIWindow? Window { get; set; }

    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene)
            return;
        Window = new UIWindow(windowScene) { RootViewController = new SceneViewController() };
        Window.MakeKeyAndVisible();
    }
}
