using System;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Data;
//using Google;

public class FirebaseAuthManager : MonoBehaviour
{

    #region Vars
    [Header("Database")]
    public FirestoreController firestoreController;

    [Space]
    [Header("Facebook")]
    [SerializeField] List<string> facebookParams;

    [Space]
    [Header("Google")]
    [SerializeField] private string googleWebApi = "";

    public FirebaseUser user;
    private FirebaseAuth firebaseAuth;
    private Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;

    #endregion

    #region Unity Callbacks
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnUserLogout();
        }
    }
    #endregion

    #region Init
    public void Awake()
    {
        //base.Awake();

        if (dependencyStatus != Firebase.DependencyStatus.Available)
        {
            InitializeFirebase();
        }
        else
        {
            firebaseAuth = FirebaseAuth.DefaultInstance;
        }

        //InitializeGoogle();

        _ = StartCoroutine(CheckForLoggedInUser()); //Uncomment for auto login
    }
    #endregion

    #region Firebase

    public void InitializeFirebase()
    {
        _ = Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                firebaseAuth = FirebaseAuth.DefaultInstance;
                firestoreController.InitializeFirestore();
            }
            else
            {
                Debug.Log("Could not resolve all Firebase dependencies: " + dependencyStatus);
            }
        });
    }

    #endregion

    #region Public methods

    private IEnumerator CheckForLoggedInUser()
    {
        yield return new WaitForSeconds(2);
        CheckLogginedInUser();
    }
    private void CheckLogginedInUser()
    {
        if (firebaseAuth.CurrentUser != null)
        {
            var userInfo = FirebaseAuth.DefaultInstance.CurrentUser;
            if (userInfo != null)
            {
                if (userInfo.ProviderId == "google.com")
                {
                    Debug.Log("Auto Login with Google Successful");
                    //return;
                }
                if (userInfo.ProviderId == "Firebase")
                {
                    Debug.Log("Auto Login with Anonymously Successful");
                    //return;
                }
            }

            user = firebaseAuth.CurrentUser;

            if (FirebaseAuth.DefaultInstance.CurrentUser.IsEmailVerified)
            {
                Debug.Log("User Logged In Successful");
            }
            else
            {
                Debug.Log("User Logged In but not verified :: " + user.UserId);
            }

            firestoreController.GetUser(user.UserId);

        }
        else
        {
            Debug.Log("User Not Logged In");
            LoginWithanonymous();
        }

        //UIManager.Instance.loadingPanel.Hide();
    }

    public void Login(string email, string password)
    {
        //_ = SigninWithEmailAsync(email, password);
    }

    public void ForgotPassword(string email)
    {
        SendPasswordResetEmail(email);
    }

    public void ResendEmail()
    {
        SendVerificationMail();
    }

    public void CreateUser(string email, string password)
    {
        _ = CreateUserWithEmailAsync(email, password);
    }

    public void LoginWithGoogle()
    {
        //GoogleLogin();
    }

    public void LoginWithanonymous()
    {
        AnonymousLogin();
    }
    #endregion

    #region FirebaseAPI

    private Task CreateUserWithEmailAsync(string email, string password)
    {
        return firebaseAuth.CreateUserWithEmailAndPasswordAsync(email, password)
          .ContinueWithOnMainThread((task) =>
          {
              if (LogTaskCompletion(task, "User Creation"))
              {
                  //SendVerificationMail(); //Use this for email verication
              }
              return task;
          }).Unwrap();
    }

    private void SendVerificationMail()
    {
        user = firebaseAuth.CurrentUser;

        _ = user.SendEmailVerificationAsync().ContinueWithOnMainThread((authTask) =>
        {
            if (LogTaskCompletion(authTask, "Send Password Reset Email"))
            {
                Debug.Log("Reset Email Password Successful");
            }
        });
    }


    //private Task SigninWithEmailAsync(string email, string password)
    //{
    //  return firebaseAuth.SignInAndRetrieveDataWithCredentialAsync(
    //        EmailAuthProvider.GetCredential(email, password)).ContinueWithOnMainThread(
    //          HandleSignInWithSignInResult);
    //}

    //private void HandleSignInWithSignInResult(Task<SignInResult> task)
    //{
    //  if (LogTaskCompletion(task, "Sign-in"))
    //  {
    //    Debug.Log("User Logged In");

    //    user = firebaseAuth.CurrentUser;

    //    firestoreController.GetUser(user.UserId);
    //  }
    //  else
    //  {
    //    Debug.Log("User Data Not Found");
    //  }
    //  //UIManager.Instance.loadingPanel.Hide();
    //}

    private void SendPasswordResetEmail(string email)
    {
        _ = firebaseAuth.SendPasswordResetEmailAsync(email).ContinueWithOnMainThread((authTask) =>
        {
        });
    }

    private bool LogTaskCompletion(Task task, string operation)
    {
        bool complete = false;

        if (task.IsCanceled)
        {
            Debug.Log("User Data Not Found");
            //userInputs.popup.PopupMessage("Password Reset", operation + " canceled.");
        }
        else if (task.IsFaulted)
        {
            Debug.Log("User Data Not Found");
            foreach (Exception exception in task.Exception.Flatten().InnerExceptions)
            {
                string authErrorCode = "";
                Firebase.FirebaseException firebaseEx = exception as Firebase.FirebaseException;
                if (firebaseEx != null)
                {
                    //authErrorCode = String.Format("AuthError.{0}: ",
                    //  ((Firebase.Auth.AuthError)firebaseEx.ErrorCode).ToString());
                }
                string[] error = exception.ToString().Split(":");
                //userInputs.popup.PopupMessage("Error", error[1]);
            }
        }
        else if (task.IsCompleted)
        {
            complete = true;

            Debug.Log("User Created/Logged In With Email/Password");
        }
        return complete;
    }


    public void OnUserLogout()
    {
        firebaseAuth.SignOut();
    }
    #endregion

    #region Anonymous
    private void AnonymousLogin()
    {
        firebaseAuth.SignInAnonymouslyAsync().ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("SignInAnonymouslyAsync was canceled.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("SignInAnonymouslyAsync encountered an error: " + task.Exception);
                return;
            }

            AuthResult result = task.Result;
            Debug.LogFormat("User signed in successfully: {0} ({1})",
                result.User.DisplayName, result.User.UserId);
        });
    }
    #endregion

    #region Google

    //private void InitializeGoogle()
    //{
    //  configuration = new GoogleSignInConfiguration {
    //    WebClientId = googleWebApi,
    //    RequestIdToken = true
    //  };
    //}

    //private void GoogleLogin()
    //{
    //  UIManager.Instance.loadingPanel.Show();

    //  GoogleSignIn.Configuration = configuration;
    //  GoogleSignIn.Configuration.UseGameSignIn = false;
    //  GoogleSignIn.Configuration.RequestIdToken = true;
    //  GoogleSignIn.Configuration.RequestEmail = true;
    //  GoogleSignIn.Configuration.RequestProfile = true;


    //  _ = GoogleSignIn.DefaultInstance.SignIn().ContinueWithOnMainThread((authTask) => {
    //    Debug.Log("Google Authentication completed");
    //    OnGoogleAuthenticatedFinished(authTask);
    //  });
    //}
    //private void OnGoogleAuthenticatedFinished(Task<GoogleSignInUser> obj)
    //{
    //  if (obj.IsFaulted)
    //  {
    //    Debug.LogError("Google login Fault");
    //    UIManager.Instance.loadingPanel.Hide();
    //    return;
    //  }
    //  else if (obj.IsCanceled)
    //  {
    //    Debug.Log("Google login cancelled");
    //    UIManager.Instance.loadingPanel.Hide();
    //    return;
    //  }
    //  else
    //  {
    //    var credential = GoogleAuthProvider.GetCredential(obj.Result.IdToken, null);
    //    GoogleFirebaseLogin(credential);
    //  }
    //}

    //private void GoogleFirebaseLogin(Credential credential)
    //{
    //  _ = firebaseAuth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
    //  {
    //    //UIManager.Instance.loadingPanel.Hide();

    //    if (task.IsCanceled)
    //    {
    //      Debug.Log("Signin Cancelled");
    //      UIManager.Instance.loadingPanel.Hide();
    //      return;
    //    }
    //    else if (task.IsCanceled)
    //    {
    //      Debug.Log("Signing Cancelled");
    //      UIManager.Instance.loadingPanel.Hide();
    //      return;
    //    }

    //    Debug.Log("Google Sign In Successful");
    //    user = firebaseAuth.CurrentUser;
    //    UIManager.Instance.loginPanel.OnHideView();
    //    UIManager.Instance.mainMenuPanel.OnShowView();

    //    firestoreController.GetUser(user.UserId);

    //    SocketNetworkManager.Instance.isLoggedIn = true;

    //    SocketNetworkManager.Instance.Emit_JoinLobby();
    //  });
    //}

    #endregion
}
