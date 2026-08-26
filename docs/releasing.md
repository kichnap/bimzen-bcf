*English · [Русский](releasing.ru.md)*

# Releasing

`BimZen.Bcf.Core` is published to nuget.org by
[`.github/workflows/release.yml`](../.github/workflows/release.yml), triggered
by a version tag.

There is no API key in this repository and there should not be one. nuget.org
exchanges a GitHub OIDC token for a temporary key that lives for an hour, so
nothing long-lived is stored anywhere. nuget.org itself
[discourages API keys](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
for automated publishing now.

## One-time setup

Done once by the account that owns the package.

1. **The trusted publishing policy**, at nuget.org under your username →
   **Trusted Publishing** → add a policy:

   | Field | Value |
   | --- | --- |
   | Repository Owner | `kichnap` |
   | Repository | `bimzen-bcf` |
   | Workflow File | `release.yml` |
   | Environment | leave empty |

   The workflow file is the **name only**, without `.github/workflows/`.
   Renaming the workflow breaks publishing until the policy is updated to
   match — which is the point of it.

   A policy for a public repository is active straight away. For a private
   one it starts out temporarily active for seven days: nuget.org needs the
   repository and owner IDs, which arrive with the first successful publish,
   to pin the policy against a repository being deleted and recreated under
   the same name. The seven-day window can be restarted at any time.

2. **The `NUGET_USER` variable**, in this repository under
   Settings → Secrets and variables → Actions → Variables. Its value is the
   nuget.org **profile name**, not an email address. It is a variable rather
   than a secret on purpose: it is not a credential, and a missing secret
   fails the login step without saying why.

   If the policy is owned by an organisation rather than by a person, the
   value is still the profile name of the account that publishes.

## Releasing a version

1. Fill in `CHANGELOG.md` and `CHANGELOG.ru.md` — move `[Unreleased]` under
   the new number.
2. Set `<Version>` in `Bcf.Core/Bcf.Core.csproj`.
3. Commit, then tag and push:

   ```bash
   git tag v1.1.0 && git push origin main --tags
   ```

The workflow refuses to publish when the tag and `<Version>` disagree, runs
the tests, packs, asks nuget.org for a temporary key and pushes. The symbol
package (`.snupkg`) goes with it; the `.nupkg` is also kept as a build
artifact, so a failed push can be examined without a rebuild.

`--skip-duplicate` is deliberate: re-running a workflow over a version that
is already on nuget.org should be a no-op, not a red build.

## When something goes wrong

**The login step fails without an explanation.** Most often `id-token: write`
is missing from the job, and GitHub then issues no token at all. It is in the
workflow — check that an edit did not drop it.

**nuget.org rejects the token.** The policy no longer matches: the workflow
file was renamed, the repository was moved, or the policy was created against
a different owner. Policies are also deactivated when the person who created
an organisation-owned policy leaves that organisation, and come back when
they are added again.

**A version is already published.** Versions on nuget.org cannot be replaced,
only unlisted. Release the next number.
