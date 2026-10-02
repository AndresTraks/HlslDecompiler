vs_3_0
dcl_position v0
dcl_position o0
dcl_texcoord o1
def c0, 2, 3, 5, 7
def c1, 11, 13, 17, 19
def c2, 23, 29, 31, 37
def c4, 41, 43, 47, 53
def c5, 59, 61, 67, 71
def c6, 73, 79, 83, 89
def c7, 97, 101, 103, 107
def c8, 109, 113, 127, 131
def c9, 137, 139, 149, 151
m4x3 r0.xyz, v0, c0
m3x4 r1, v0.xyz, c4.xyz
m3x2 r2.xy, v0.xyz, c8.xyz
mov o0.xyz, r0.xyz
mov o0.w, r1.w
mov o1.xy, r2.xy
mov o1.zw, r1.xy
