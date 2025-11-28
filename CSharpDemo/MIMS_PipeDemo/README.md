# MIMS Pipe Demo (Routed, .NET Framework 4.6.1)

## Projects
- Hub: central router with heartbeat monitor and client registry
- ClientA/B/C/D: independent console apps, auto reconnect, heartbeat, ACK resend

## How to run
1. Open each project in Visual Studio (Framework 4.6.1).
2. Ensure **Newtonsoft.Json** is installed via NuGet (per project).
3. Start **Hub** first.
4. Start any of the clients. Use commands:
   - `send <TargetId> <Text>`: send to a specific client (e.g., `send ClientB Hi`)
   - `demo`: trigger predefined flows
     - ClientA → ClientB
     - ClientB → ClientC
     - ClientD → ClientA

## Stability scenarios
- **Hub down**: clients detect heartbeat loss (>20s), reconnect automatically with exponential backoff and re-register; pending ACK messages are re-enqueued.
- **Client down**: Hub removes mapping; when client restarts, it registers again; senders will retry until ACK.

## Notes
- NamedPipeTransmissionMode.Message is used; messages are JSON (BusMessage).
- ACK timeout is 10s; heartbeat interval is 5s; heartbeat timeout at client is 20s; Hub disconnects clients if no Ping for 20s.
- Tune constants in code if needed.
