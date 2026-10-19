vs_3_0
dcl_position v0
dcl_normal v1
dcl_tangent v2
dcl_position o0
dcl_normal o1
def c0, 1, 2, 3, 4
def c1, 5, 6, 7, 8
cnd o0, v0, v1, c0
cnd r0, v1, v2, v0
mov o1, r0
