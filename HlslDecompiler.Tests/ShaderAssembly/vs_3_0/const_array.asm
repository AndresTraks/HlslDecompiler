vs_3_0
def c1, 1, 0, 0, 0
def c2, 0, 1, 0, 0
def c3, 0, 0, 1, 0
def c4, 1, 1, 0, 0
dcl_position o0
mova a0.x, c0.x
mov r0.xyz, c1.xyz
mad o0, c1[a0.x].xyzx, r0.xxxy, r0.yyzx
