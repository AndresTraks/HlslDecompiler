vs_3_0
def c2, 0, 1, 0, 0
dcl_position v0
dcl_position o0
mul r0.xy, c1.xy, v0.yy
mad o0.xy, c0.xy, v0.xx, r0.xy
mov o0.zw, c2.xy
