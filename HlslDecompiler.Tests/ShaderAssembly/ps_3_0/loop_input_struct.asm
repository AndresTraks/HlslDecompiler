ps_3_0
dcl_color v0
dcl_texcoord v1.x
mov r0, v0
rep i0
mad r0, c0, v1.x, r0
endrep
mov oC0, r0
