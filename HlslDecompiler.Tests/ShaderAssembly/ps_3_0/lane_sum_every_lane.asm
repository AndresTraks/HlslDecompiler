ps_3_0
dcl_texcoord v0.xy
dcl_2d s0
texld r0, v0.xy, s0
mul r1, r0.wxyz, c0.wxyz
add r1, r1.zwxy, r1.yzwx
mad r1, r0.zwxy, c0.zwxy, r1
mad oC0, r0.wxyz, c0.wxyz, r1
