import Foundation

protocol TelemetryReporting: Sendable {
    func info(_ message: String, metadata: [String: String])
    func warning(_ message: String, metadata: [String: String])
    func error(_ message: String, metadata: [String: String])
}

extension TelemetryReporting {
    func info(_ message: String) {
        info(message, metadata: [:])
    }

    func warning(_ message: String) {
        warning(message, metadata: [:])
    }

    func error(_ message: String) {
        error(message, metadata: [:])
    }
}

protocol TelemetryTransport: Sendable {
    nonisolated func send(_ request: URLRequest)
}

struct URLSessionTelemetryTransport: TelemetryTransport {
    nonisolated func send(_ request: URLRequest) {
        URLSession.shared.dataTask(with: request).resume()
    }
}

final class AppTelemetry: TelemetryReporting, @unchecked Sendable {
    static let shared = AppTelemetry()

    private let queue = DispatchQueue(label: "telemetry.queue", qos: .utility)
    private let logFileURL: URL
    private let transport: any TelemetryTransport

    init(transport: any TelemetryTransport = URLSessionTelemetryTransport()) {
        self.transport = transport
        let fileManager = FileManager.default
        let appSupport = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? URL(fileURLWithPath: NSTemporaryDirectory())
        let logDirectory = appSupport
            .appendingPathComponent("DungeonSoundboard", isDirectory: true)
            .appendingPathComponent("Logs", isDirectory: true)
        try? fileManager.createDirectory(at: logDirectory, withIntermediateDirectories: true)
        logFileURL = logDirectory.appendingPathComponent("telemetry.log")
    }

    func installCrashHandlers() {
        NSSetUncaughtExceptionHandler { exception in
            AppTelemetry.shared.error(
                "Uncaught exception: \(exception.name.rawValue)",
                metadata: [
                    "reason": exception.reason ?? "n/a",
                    "stack": exception.callStackSymbols.joined(separator: " | ")
                ]
            )
        }
    }

    func info(_ message: String, metadata: [String: String] = [:]) {
        write(level: "info", message: message, metadata: metadata)
    }

    func warning(_ message: String, metadata: [String: String] = [:]) {
        write(level: "warning", message: message, metadata: metadata)
    }

    func error(_ message: String, metadata: [String: String] = [:]) {
        write(level: "error", message: message, metadata: metadata)
        sendToSentryIfEnabled(message: message, metadata: metadata)
    }

    private func write(level: String, message: String, metadata: [String: String]) {
        queue.async { [logFileURL] in
            let event: [String: Any] = [
                "id": UUID().uuidString,
                "timestamp": ISO8601DateFormatter().string(from: Date()),
                "level": level,
                "message": message,
                "metadata": metadata
            ]
            guard let data = try? JSONSerialization.data(withJSONObject: event, options: []),
                  let line = String(data: data, encoding: .utf8) else {
                return
            }
            let output = line + "\n"

            if FileManager.default.fileExists(atPath: logFileURL.path) {
                if let handle = try? FileHandle(forWritingTo: logFileURL) {
                    defer { try? handle.close() }
                    handle.seekToEndOfFile()
                    if let payload = output.data(using: .utf8) {
                        try? handle.write(contentsOf: payload)
                    }
                }
            } else {
                try? output.write(to: logFileURL, atomically: true, encoding: .utf8)
            }
        }
    }

    private func sendToSentryIfEnabled(message: String, metadata: [String: String]) {
        let defaults = UserDefaults.standard
        guard defaults.bool(forKey: PlayerDefaultsKeys.sentryEnabled),
              let dsn = defaults.string(forKey: PlayerDefaultsKeys.sentryDSN),
              !dsn.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return
        }

        guard let request = sentryRequest(dsn: dsn, message: message, metadata: metadata) else {
            return
        }

        queue.async { [transport] in
            transport.send(request)
        }
    }

    private func sentryRequest(dsn: String, message: String, metadata: [String: String]) -> URLRequest? {
        guard let dsnURL = URL(string: dsn),
              let host = dsnURL.host,
              let user = dsnURL.user else {
            return nil
        }

        let projectID = dsnURL.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        guard !projectID.isEmpty else { return nil }
        guard let endpoint = URL(string: "https://\(host)/api/\(projectID)/store/?sentry_version=7&sentry_key=\(user)") else {
            return nil
        }

        struct Payload: Encodable {
            let event_id: String
            let level: String
            let timestamp: String
            let logger: String
            let platform: String
            let message: String
            let extra: [String: String]
        }

        let payload = Payload(
            event_id: UUID().uuidString.replacingOccurrences(of: "-", with: ""),
            level: "error",
            timestamp: ISO8601DateFormatter().string(from: Date()),
            logger: "DungeonSoundboard",
            platform: "native",
            message: message,
            extra: metadata
        )

        guard let data = try? JSONEncoder().encode(payload) else { return nil }
        var request = URLRequest(url: endpoint)
        request.httpMethod = "POST"
        request.httpBody = data
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        return request
    }
}
