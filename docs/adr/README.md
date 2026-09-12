# Architecture Decision Records

Lightweight records of significant technical decisions. Format: Status / Context /
Decision / Consequences. New decisions take the next number.

| ID   | Title                                        | Status   |
|------|----------------------------------------------|----------|
| 0001 | Core stack & identity model                  | Accepted |
| 0002 | Deployment topology: one instance per neighborhood | Accepted |
| 0003 | Roles & moderator scoping                    | Accepted |
| 0004 | Data persistence & schema evolution          | Accepted |
| 0005 | Multilingual support (UI & static pages)     | Accepted |
| 0006 | Module boundary contracts (Identity, UserInfo, Authorization) | Accepted |
| 0007 | Group management lane: owner ∪ GlobalAdmin for add and remove   | Accepted |
| 0008 | Member self-leave lane (owner excepted)   | Accepted |
| 0009 | Group description: resident-facing display + owner ∪ GlobalAdmin edit | Accepted |
| 0010 | Private groups: a membership/organizing unit, hidden from the audience pickers | Accepted |
| 0011 | Media & file storage: content-addressed local-volume bytes + a `mt` catalog | Accepted |
| 0012 | Community membership: mandatory communities + moderator-managed optional membership | Accepted |
| 0013 | Group posts: a membership-scoped group channel | Accepted |
| 0014 | Post edit lane: author-only (no moderator/admin branch) | Accepted |
