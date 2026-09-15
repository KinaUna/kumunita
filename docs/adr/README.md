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
| 0015 | UI view-localization mechanics (TagHelper + curated registry) | Accepted |
| 0016 | Group-post + reply edit lane: author-only (no moderator/admin branch) | Accepted |
| 0017 | Announcement edit lane: author-of-record ∪ GlobalAdmin (flat lane) | Accepted |
| 0018 | UGC authored-in language tag (posts, replies, announcements) | Accepted |
| 0019 | Timezone: platform default (admin) + per-resident override | Accepted |
| 0020 | Date & time format: platform default (admin) + per-resident override + custom | Accepted |
| 0021 | Translator role: delegate translation editing to non-admin residents | Accepted |
| 0022 | User-added post & reply translations (the lane ADR 0018 deferred) | Accepted |
| 0023 | Reply as a report target (extending the M3b report lane) | Accepted |
| 0024 | Author soft-delete lane (posts + replies, community + group) | Accepted |
| 0025 | Rich content: Markdown + content images | Accepted |
| 0026 | Group & community name/description translations | Accepted |
| 0027 | Post & reply translation display & swap: authored-in as a first-class variant + click-to-swap | Accepted |
| 0028 | Guardian controls: account-scope supervision of a child's account (suspend, communities/groups, invitation approval; no content reads) | Accepted |
| 0029 | Announcement user-added translations (GlobalAdmin/Translator any; community-Moderator their community's; add-only) | Accepted |
| 0030 | Role independence: elevated roles (GlobalAdmin / Moderator / Translator) are composable per resident, not mutually exclusive | Accepted |
| 0031 | WYSIWYG editor + toolbar over the RC Markdown lane (split-view live preview + Markdown-splice toolbar, `tsc`-only; Amends 0025) | Accepted |
