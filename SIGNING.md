# Code signing status and policy

Seer 1.0.0 is an unsigned public preview. Its GitHub release is available, but
Windows Smart App Control may block installation because the publisher cannot
be verified. There is no per-app allow switch. Users should not turn off
device-wide protection to install Seer.

The goal is to publish a signed installer and signed app binaries from a
verifiable build of this open-source repository. The project is seeking a
trusted signing provider. No signing provider has approved Seer yet, and no
release should be described as signed until its Authenticode signatures have
been verified. Signing a downloaded `Setup.exe` afterward is insufficient:
Velopack must sign its app, update components, and installer during packaging.

The repository owner, `localinvestorsusa-create`, maintains the source and
approves releases. Source changes and packaging scripts are reviewed before
release. The planned signing process will use the public source repository as
the build origin, and every signing request will require approval. The exact
provider-specific policy and team roles will be recorded here when a signing
service accepts the project.

See [Privacy](PRIVACY.md) for network and recording behavior.
