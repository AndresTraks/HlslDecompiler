ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_texture2d (uint,uint,uint,uint) t0
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_output o0
dcl_temps 3
ftoi r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(uint,uint,uint,uint) r0.xy, r0.xy, t0.xy
iadd r0, r0.xyxy, -cb0[0].x
xor r1.xy, r0.zw, l(2, 2, 0, 0)
and r1.xy, r1.xy, l(-2147483648, -2147483648, 0, 0)
imax r2, r0, -r0.zwzw
iadd r0.x, -r0.w, r0.z
ushr r0.yz, r2.xy, l(0, 1, 1, 0)
ilt r0.w, r2.w, r2.z
and o0.z, r0.w, l(1)
ineg r1.zw, r0.yz
movc o0.xy, r1.xy, r1.zw, r0.yz
ilt r0.y, l(0), r0.x
ilt r0.x, r0.x, l(0)
iadd o0.w, -r0.y, r0.x
ret
