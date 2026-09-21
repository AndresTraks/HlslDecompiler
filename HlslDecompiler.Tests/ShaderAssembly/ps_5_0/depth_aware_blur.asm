ps_5_0
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }
dcl_constantbuffer CB0[5], dynamicIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v1.xy
dcl_output o0
dcl_temps 5
sample_indexable(texture2d)(float,float,float,float) r0.x, v1.x, t1.x, s0
ge r0.y, r0.x, l(1)
discard_nz r0.y
ineg r0.y, cb0[0].z
dp2 r0.z, cb0[0].ww, cb0[0].ww
mov r1.y, l(0)
mov r2, l(0, 0, 0, 0)
mov r0.w, l(0)
mov r1.z, r0.y
loop
ilt r1.w, cb0[0].z, r1.z
breakc_nz r1.w
itof r1.x, r1.z
mad r1.xw, r1.xy, cb0[0].xy, v1.xy
sample_indexable(texture2d)(float,float,float,float) r3.x, r1.x, t1.x, s0
add r3.x, -r0.x, r3.x
lt r3.x, l(0.00999999978), |r3.x|
if_nz r3.x
iadd r3.x, r1.z, l(1)
mov r1.z, r3.x
continue
endif
imax r3.x, -r1.z, r1.z
and r3.y, r3.x, l(3)
ushr r3.x, r3.x, l(2)
dp4 r3.x, cb0[r3.x + 1], icb[r3.y]
imul null, r3.y, r1.z, -r1.z
itof r3.y, r3.y
div r3.y, r3.y, r0.z
mul r3.y, r3.y, l(1.44269502)
exp r3.y, r3.y
mul r3.z, r3.y, r3.x
sample_indexable(texture2d)(float,float,float,float) r4, r1.xwxx, t0, s0
mad r2, r4, r3.z, r2
mad r0.w, r3.x, r3.y, r0.w
iadd r1.z, r1.z, l(1)
endloop
lt r0.x, l(0), r0.w
div r1, r2, r0.w
sample_indexable(texture2d)(float,float,float,float) r2, v1.xyxx, t0, s0
movc o0, r0.x, r1, r2
ret
