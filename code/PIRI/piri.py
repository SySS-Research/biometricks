#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""
  PIRI - Process IR Image

  Simple Python tool to modify near IR images for bypassing biometric
  face authentication systems

  MIT License

  Copyright (c) 2017, 2018 Matthias Deeg, SySS GmbH

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in
  all copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
"""

__version__ = '0.3'
__author__ = 'Matthias Deeg'

import argparse
from PIL import Image, ImageEnhance

# some default values
DEFAULT_BRIGHTNESS_FACTOR = 1.18            # 18% enhanced brightness (simple algorithm)
DEFAULT_CONTRAST_FACTOR = 1.16              # 16% enhanced contrast (Pillow algorithm)
DEFAULT_ALPHA = 0.2                         # 20% opacity


def saturate(v, threshold=255):
    """Saturation function"""
    if v > threshold:
        v = threshold
    if v < 0:
        v = 0

    return int(v)


def simple_brightness_grayscale(im, factor):
    """Simple brightness function for grayscale images"""

    data = im.load()
    v = int(256 * (factor - 1))

    for x in range(im.width):
        for y in range(im.height):
            data[x, y] = saturate(data[x, y] + v)


def simple_brightness_rgba(im, factor):
    """Simple brightness function for RGBA images"""

    data = im.load()
    v = int(256 * (factor - 1))

    for x in range(im.width):
        for y in range(im.height):
            c = data[x, y]
            r = saturate(c[0] + v, 255)
            g = saturate(c[1] + v, 255)
            b = saturate(c[2] + v, 255)
            data[x, y] = (r, g, b)


def simple_contrast_grayscale(im, factor):
    """Simple contrast function for grayscale images"""

    data = im.load()
    level = int(256 * (factor - 1))
    f = (259.0 * (level + 255)) / (255.0 * (259 - level))

    for x in range(im.width):
        for y in range(im.height):
            c = saturate(f * (data[x, y] - 128) + 128)
            data[x, y] = c


def simple_contrast_rgba(im, factor):
    """Simple contrast function for RGBA images"""

    data = im.load()
    level = int(256 * (factor - 1))
    f = (259.0 * (level + 255)) / (255.0 * (259 - level))

    for x in range(im.width):
        for y in range(im.height):
            c = data[x, y]
            r = saturate(f * (c[0] - 128) + 128)
            g = saturate(f * (c[1] - 128) + 128)
            b = saturate(f * (c[2] - 128) + 128)
            data[x, y] = (r, g, b)


# main program
if __name__ == '__main__':

    print("PIRI v{} by Matthias Deeg (c) SySS GmbH 2017".format(__version__))

    # init argument parser
    parser = argparse.ArgumentParser()
    parser.add_argument('-i', '--input', type=str, help='Input image', required=True)
    parser.add_argument('-o', '--output', type=str, help='Output image', default='output.png', required=False)
    parser.add_argument('-b', '--brightness-factor', type=float, help='Brightness factor (default is {}'.format(DEFAULT_BRIGHTNESS_FACTOR), default=DEFAULT_BRIGHTNESS_FACTOR, required=False)
    parser.add_argument('-c', '--contrast-factor', type=float, help='Contrast factor (default is {})'.format(DEFAULT_CONTRAST_FACTOR), default=DEFAULT_CONTRAST_FACTOR, required=False)
    parser.add_argument('-a', '--alpha', type=float, help='Alpha value for red layer (default is {})'.format(DEFAULT_ALPHA), default=DEFAULT_ALPHA, required=False)

    # parse arguments
    args = parser.parse_args()

    # open image and convert to RGBA format
    image = Image.open(args.input).convert("RGBA")

    print("[*] Loaded image '{}'".format(args.input))

    # enhance brightness using a simple brightness algorithm
    simple_brightness_rgba(image, args.brightness_factor)

    # enhance contrast with simple contrast algorithm
    simple_contrast_rgba(image, args.contrast_factor)

    # save enhanced image without red layer
    filename = "grayscale_{}".format(args.output)
    image.save(filename)
    print("[*] Saved processed image for grayscale print to '{}'".format(filename))

    # add transparent red layer
    red_layer = Image.new("RGBA", image.size)
    red_layer.paste((255, 0, 0, int(256 * args.alpha)), [0, 0, red_layer.size[0], red_layer.size[1]])
    result = Image.alpha_composite(image, red_layer)

    # save processed image
    filename = "color_{}".format(args.output)
    image.save(filename)
    result.save(filename)
    print("[*] Saved processed image with red layer for color print to '{}'".format(filename))
