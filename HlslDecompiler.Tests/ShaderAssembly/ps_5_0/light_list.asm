ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_structured t0, 32
dcl_resource_texture2d (float,float,float,float) t1
dcl_resource_texture2d (float,float,float,float) t2
dcl_resource_texture2d (float,float,float,float) t3
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 6
sample_indexable(texture2d)(float,float,float,float) r0.xyz, v0.xyx, t1.xyz, s0
sample_indexable(texture2d)(float,float,float,float) r1.xyz, v0.xyx, t2.xyz, s0
dp3 r0.w, r1.xyz, r1.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, r1.xyz
sample_indexable(texture2d)(float,float,float,float) r2.xyz, v0.xyx, t3.xyz, s0
mov r3.xyz, l(0, 0, 0, 0)
mov r0.w, l(0)
loop
uge r1.w, r0.w, cb0[0].x
breakc_nz r1.w
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r4, r0.w, l(0), t0
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r5.xyz, r0.www, l(16), t0.xyz
add r4.xyz, -r2.xyz, r4.xyz
dp3 r1.w, r4.xyz, r4.xyz
sqrt r1.w, r1.w
div r4.xyz, r4.xyz, r1.www
dp3_sat r2.w, r1.xyz, r4.xyz
mul r4.xyz, r2.www, r5.xyz
div r1.w, r1.w, r4.w
add_sat r1.w, -r1.w, l(1)
mad r3.xyz, r4.xyz, r1.www, r3.xyz
iadd r0.w, r0.w, l(1)
endloop
mul o0.xyz, r0.xyz, r3.xyz
mov o0.w, l(1)
ret
