RelativeEmbed allows you to invisibly hide (encrypted) data in an image by very subtly adjusting the color values of some pixels. You can then use RelativeExtract with the original image as reference to extract the data again.

# Usage

## General

The .exe files can be used as .NET libraries if you want to integrate them into another program. (You can rename them to .dll or just add them directly as .exe references.) All relevant functions should be exported.

## RelativeEmbed

RelativeEmbed.exe [options] \<inputFile\> \<fileToEmbed\> \<outputFile\>

The following options are supported:

* -k [key], --key [key]: Use the specified encryption key
* -c [type], --content [type]: Specify content type: 'file', 'text' or 'data'
* -o [offset], --offset [offset]: Offset in fileToEmbed to start reading at (0 by default)
* -l [bytes], --length [bytes]: Number of bytes in fileToEmbed to read; use 0 to read everything (0 by default)
* -v, --verbose: Print verbose output
* -s, --silent: Do not print any output (will still print output if invalid arguments are provided, but not on error)

If no key is specified, the key 'SecureBeneathTheWatchfulEyes' will be used by default. Explicitly specify an empty key ("") if you want to embed the data unencrypted.

It should go without saying that if you don't want anyone stumbling upon the image(s) to be able to extract the contents, you should specify a custom key rather than using the default.

## RelativeExtract

Usage: RelativeExtract.exe [options] \<inputFile\> \<referenceFile\> [outputFile]

The following options are supported:

* -k [key], --key [key]: Use the specified encryption key
* -a [inputFile], --append [inputFile]: Also extract another file and append it to the result (can be repeated)
* -p, --print: Display the embedded text instead of writing to a file
* -v, --verbose: Print verbose output
* -s, --silent: Do not print any output other than --print output (will still print output if invalid arguments are provided, but not on error)

If no key is specified, the key 'SecureBeneathTheWatchfulEyes' will be used by default. Explicitly specify an empty key ("") if you want to extract unencrypted data.

# How it works and format spec

RelativeEmbed works by storing the embedded data bit by bit in the RGB values of non-transparent (that is, alpha > 0) pixels of an image. A number of RGB pixel values equal to the data size is randomly chosen throughout the image and then raised or lowered by 1 or 2 to indicate 0 bits and 1 bits. RelativeExtract then compares the exact color values of non-transparent (alpha > 0) pixels starting at the top left, going left to right and top to bottom:

* If a color value matches, ignore it.
* If a color value is 1 higher or 1 lower than in the reference image, add a 0 bit.
* If a color value is 2 higher or 2 lower than in the reference image, add a 1 bit.
* If the difference is higher than 2, ignore it.

The result is an image with generally imperceptible distortions which stores one byte of data per 8 distortions. (This may sound like a lot, but it really is imperceptible on most images.) The amount of data in bytes an image with no transparency can hold can be calculated using: width * height * 3 / 8. For example, a 1920x1080 screenshot can hold 777600 bytes (759 KB) of hidden data.

Unless an empty key is explicitly specified, the data will be encrypted using AES256. The first 16 bytes of the data will be the IV. After that follows encrypted data. It starts with the header:

* (1 byte) Format version, currently 0x01
* (1 byte) Content type:
* * 0x00 - Invalid
* * 0x01 - Data
* * 0x02 - File
* * 0x03 - Text
* (4 bytes) Length of (compressed) data
* (16 bytes) MD5 hash of (compressed) data

Beyond that is the data, with compressed length as specified in the header, compressed using the DEFLATE algorithm. It may be (and is by default) followed by unencrypted random data. If an empty key is used, the IV is skipped and the embeded data starts immediately with the format version. It is otherwise identical, save for being unencrypted.

## Encryption details

The encryption used is AES256 with a randomly generated IV (16 bytes), which is prepended to the encrypted data. The key used for the encryption is derived from the user-provided key (or 'SecureBeneathTheWatchfulEyes' if none is provided), hashed using the PBKDF2 algorithm with 4854 cycles using the salt 41726775696e67207468617420796f7520646f6e277420636172652061626f75742074686520726967687420746f2070726976616379206265636175736520796f752068617665206e6f7468696e6720746f2068696465206973206e6f20646966666572656e74207468616e20736179696e6720796f7520646f6e277420636172652061626f7574206672656520737065656368206265636175736520796f752068617665206e6f7468696e6720746f207361792e ('Arguing that you don't care about the right to privacy because you have nothing to hide is no different than saying you don't care about free speech because you have nothing to say.'). Note that data beyond the indicated length may (and likely will) be random and whatever function you use for decryption might break if you read beyond the encrypted data.

# Examples

Using the following image as reference:

![happy](https://user-images.githubusercontent.com/1906108/227748236-952ada5d-cc92-4b72-9448-c5369d1f0264.jpg)
<sub>(Image source: Onimai episode 2)</sub>

You can extract the source and binaries of the RelativeEmbed tools as well as the TransparentEmbed tools from the following image using the default key:

![happy_embed](https://user-images.githubusercontent.com/1906108/227748286-d61056da-93d3-45f7-b402-8b4b4e2ca142.png)

* The image was created by running: RelativeEmbed.exe happy.jpg embeddingtools.zip happy_embed.png
* The data can be extracted using: RelativeExtract.exe happy_embed.png happy.jpg output.zip

The most obvious use case is providing the reference image alongside the data image, but there are various ways to avoid this. Some examples:

Contextual clues could provide a receiver with a way to retrieve the original data. For example, posting the following image with the filename '77beb20b2538d78c51e7a4167c83955b.png' allows someone to [search booru sites for the original image using the provided MD5 hash](https://danbooru.donmai.us/posts?tags=md5%3A77beb20b2538d78c51e7a4167c83955b) and use it as reference:

![77beb20b2538d78c51e7a4167c83955b](https://user-images.githubusercontent.com/1906108/227748715-306fba75-87b0-442c-8fe6-72dad9446b3d.png)
<sub>(Image credit: Saistes, https://twitter.com/Saistes/status/1634843570204188673)</sub>

* Creation: RelativeEmbed.exe -c text real_77beb20b2538d78c51e7a4167c83955b.jpg RelativeEmbedder.cs 77beb20b2538d78c51e7a4167c83955b.png
* Extraction: RelativeExtract.exe --print 77beb20b2538d78c51e7a4167c83955b.png real_77beb20b2538d78c51e7a4167c83955b.jpg

Sometimes, contextual clues are not even necessary. For example, the above image can be run through [a reverse image search service](https://iqdb.org/) to get the original image even without knowing the MD5. You can also use a well-known or obvious reference image, such as a website logo.

Another option is to have the reference be easily reconstructible from the data image. Take for instance a QR code:

![qr_embed](https://user-images.githubusercontent.com/1906108/227749163-b1a2acf7-94c4-46f3-8394-ceda9dff6f9f.png)

At first glance this looks like a normal QR code for Microsoft's website, but if you reduce it to pure black and white (e.g. using GIMP, Colors -> Dither -> 2 for everything) and use that as reference, you'll find it actually contains the URL to this repo.

* Creation: RelativeEmbed.exe -c text qr.png url.txt qr_embed.png
* Extraction: RelativeExtract.exe --print qr_embed.png qr_restored.png
