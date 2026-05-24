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

    public static string PasswordResetByEmail(string resetLink)
    {
        return $@"<h3>Reset your EcoTrack password</h3>
            <p>We received a request to reset the password for your account.</p>
            <p>If you didn't make this request, you can ignore this email — no changes will be made.</p>
            <p>Otherwise, click the link below to choose a new password. The link is single-use and expires soon.</p>
            <a href='{resetLink}'>Reset password</a>";
    }

    public static string NewEnterpriseRegistrationToSuperAdmin(
        string enterpriseName, string edrpou, string adminEmail)
    {
        return $@"<h3>Нова заявка на реєстрацію підприємства</h3>
            <p><b>Назва:</b> {enterpriseName}</p>
            <p><b>ЄДРПОУ:</b> {edrpou}</p>
            <p><b>Email адміна:</b> {adminEmail}</p>
            <p>Перевірте ЄДРПОУ у реєстрі: <a href='https://usr.minjust.gov.ua/content/free-search/all'>usr.minjust.gov.ua</a></p>
            <p>Після перевірки погодьте або відхиліть заявку в адмін-панелі.</p>";
    }

    public static string EnterpriseApprovedToAdmin(string enterpriseName)
    {
        return $@"<h3>Реєстрацію підприємства підтверджено</h3>
            <p>Вашу заявку на реєстрацію підприємства <b>{enterpriseName}</b> підтверджено.</p>
            <p>Тепер ви можете увійти у застосунок.</p>";
    }

    public static string EnterpriseRejectedToAdmin(string enterpriseName, string reason)
    {
        return $@"<h3>Реєстрацію відхилено</h3>
            <p>Вашу заявку на реєстрацію підприємства <b>{enterpriseName}</b> відхилено.</p>
            <p><b>Причина:</b> {reason}</p>
            <p>Якщо ви вважаєте, що це помилка, зверніться до підтримки.</p>";
    }
}