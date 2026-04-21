namespace ClaudeAgentSdk.Internal;

/// <summary>
/// Pre-flight validation for ClaudeAgentOptions.SessionStore combinations.
/// </summary>
internal static class SessionStoreValidation
{
    internal static bool StoreImplements(ISessionStore store, string methodName)
    {
        var method = store.GetType().GetMethod(methodName + "Async");
        if (method == null)
            return false;

        // Check if the method is the default interface implementation
        var interfaceMap = store.GetType().GetInterfaceMap(typeof(ISessionStore));
        for (var i = 0; i < interfaceMap.InterfaceMethods.Length; i++)
        {
            if (interfaceMap.InterfaceMethods[i].Name == methodName + "Async")
            {
                // If the target method is the same as the declaring type's method,
                // it's a real implementation (not the default)
                return interfaceMap.TargetMethods[i].DeclaringType != typeof(ISessionStore);
            }
        }

        return true;
    }

    internal static void Validate(ClaudeAgentOptions options)
    {
        var store = options.SessionStore;
        if (store == null)
            return;

        if (options.ContinueConversation && options.Resume == null && !StoreImplements(store, "ListSessions"))
        {
            throw new ArgumentException(
                "continue_conversation with session_store requires the store to implement ListSessionsAsync()");
        }

        if (options.EnableFileCheckpointing)
        {
            throw new ArgumentException(
                "session_store cannot be combined with enable_file_checkpointing " +
                "(checkpoints are local-disk only and would diverge from the mirrored transcript)");
        }
    }
}
