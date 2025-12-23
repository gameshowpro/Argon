# GameshowPro.Argon
This is a set of .NET 10.0 assemblies to manage software licensing bound to secrets stored on the client system's TPM. The system is designed to be independent of the scope of applications to be licensed, along with their associated secrets. That allows this repository to be public, while the builds are created in downstream repositories that have access to the secrets they need to establish their own licensing scope. Details of this build process are [here](/docs/build.md).
## GameshowPro.Argon.Client
The CLI application can be used to create a client profile to be consumed by `GameshowPro.Argon.Create`.
## GameshowPro.Argon.Create
This CLI application can be used to encrypt license files to be consumed by client appliation using `GameshowPro.Argon.Common`. It is agnistic to the content of the license files. That is a matter for the client application.
## GameshowPro.Argon.Common
This is a library (or package) that can be referenced from a client app. It allows decryption of a given licence file into a string. Successful decryption indicates the the license is matched to the secret stored in the client machine's TPM and that the system clock value is plausible. It is the resposibility of the client application to determine which features are available at the current clock time.
## GameshowPro.Argon.Test
This is a CLI application that can create then consume a license file immediately, ensuring that the encryption features work correctly on the given machine. It's not a traditional test project, because it requires the use of a TPM, which is not possible on a build server.
## GameshowPro.Argon.Service
The TPM clock is not guaranteed to update in real time. Its only guarantee is that is cannot be updated to a value earlier than its previous value (aka ratcheting). From testing, it appears that Intel Chipsets stay up-to-date, but AMD chipsets must be updated by client code. If the clock gets too far behind, rules in `GameshowPro.Argon.Common` will cause license encryption to fail. This service runs in the background and attempts to update the TPM time regularly to ensure that it never falls behind. In the case of a system that keeps its own time, this service's calls will often fail because they represent a tiny step backwards. That's not a bad thing; it mean means the belt is already working, so the suspenders did not need to act.

## License
Copyright (C) 2025 Hamish Barjonas, Barjonas LLC

This project is licensed under the GNU Lesser General Public License v3.0. See the [LICENSE](LICENSE) file for details.