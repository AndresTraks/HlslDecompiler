vs_3_0
dcl_position v0
dcl_texcoord v1
dcl_position o0
dcl_texcoord o1
def c0, 2, 3, 5, 7
def c1, 11, 13, 17, 19
def c2, 23, 29, 31, 37
def c3, 41, 43, 47, 53
def c4, 59, 61, 67, 71
def c5, 73, 79, 83, 89
def c6, 97, 101, 103, 107
m4x4 o0, v0, c0
m3x3 o1.xyz, v1.xyz, c4.xyz
mov o1.w, c6.w
