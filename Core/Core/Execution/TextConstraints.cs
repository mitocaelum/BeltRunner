using System;
using System.Text;

namespace BeltRunner.Core.Execution;

internal static class TextConstraints {
    public const int INTERACTION_KIND_MAX_LENGTH = 120;
    public const int INTERACTION_TITLE_MAX_LENGTH = 120;
    public const int INTERACTION_MESSAGE_MAX_LENGTH = 4000;
    public const int DIAGNOSTIC_MESSAGE_MAX_LENGTH = 4000;
    public const int CANCEL_REASON_MAX_LENGTH = 512;
    public const int FAULT_KIND_MAX_LENGTH = 120;
    public const int PUBLIC_FAULT_MESSAGE_MAX_LENGTH = 400;
    public const int ERROR_CODE_MAX_LENGTH = 64;
    public const int FAULT_ORIGIN_MAX_LENGTH = 256;
    public const int REJECTION_REASON_MAX_LENGTH = 512;

    public static string NormalizeRequired(string value, int maxLength, string paramName) {
        if( value is null ) throw new ArgumentNullException(paramName);

        return Truncate(SanitizeControlCharacters(value), maxLength);
    }

    public static string NormalizeOptional(string? value, int maxLength) {
        return Truncate(SanitizeControlCharacters(value ?? string.Empty), maxLength);
    }

    public static string? NormalizeNullable(string? value, int maxLength) {
        if( value is null ) {
            return null;
        }

        return Truncate(SanitizeControlCharacters(value), maxLength);
    }

    public static bool ContainsControlCharacters(string value) {
        if( value is null ) throw new ArgumentNullException(nameof(value));

        for( int i = 0; i < value.Length; i++ ) {
            if( char.IsControl(value[i]) ) {
                return true;
            }
        }

        return false;
    }

    private static string Truncate(string value, int maxLength) {
        if( maxLength <= 0 ) throw new ArgumentOutOfRangeException(nameof(maxLength));
        if( value.Length <= maxLength ) {
            return value;
        }

        if( maxLength <= 3 ) {
            return value.Substring(0, maxLength);
        }

        return value.Substring(0, maxLength - 3) + "...";
    }

    private static string SanitizeControlCharacters(string value) {
        if( value is null ) throw new ArgumentNullException(nameof(value));

        StringBuilder? builder = null;

        for( int i = 0; i < value.Length; i++ ) {
            char current = value[i];
            char sanitized = char.IsControl(current) ? ' ' : current;

            if( builder is null ) {
                if( sanitized == current ) {
                    continue;
                }

                builder = new StringBuilder(value.Length);
                if( i > 0 ) {
                    builder.Append(value, 0, i);
                }
            }

            builder.Append(sanitized);
        }

        return builder?.ToString() ?? value;
    }
}
