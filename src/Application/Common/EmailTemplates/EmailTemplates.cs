namespace Application.Common.EmailTemplates;

public static class EmailTemplates
{
    public static string LoginCode(string code)
    {
        return $@"
        <h3>EcoTrack Login Code</h3>
        <p>You requested to sign in to your EcoTrack account.</p>
        <p>Your verification code is:</p>
        <h2>{code}</h2>
        <p>Enter this code in the application to continue.</p>
        <p>If you did not request this code, you can safely ignore this email.</p>";
    }

    public static string EmailConfirmation(string code)
    {
        return $@"
        <h3>Confirm your EcoTrack account</h3>
        <p>Please confirm your email address to activate your account.</p>
        <p>Your confirmation code is:</p>
        <h2>{code}</h2>
        <p>Enter this code in the application to complete your registration.</p>";
    }
    

    public static string InvitationByEmail(string inviteLink)
    {
        return $@"<h3>Welcome to EcoTrack!</h3>
            <p>You have been invited to join the enterprise system.</p>
            <p>Click the link below to complete your registration:</p>
            <a href='{inviteLink}'>Join Now</a>";
    }
}