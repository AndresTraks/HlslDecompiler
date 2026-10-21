ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB3[2], immediateIndexed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_body fb2
dcl_function_body fb3
dcl_function_table ft0 = {fb0, fb2}
dcl_function_table ft1 = {fb1, fb3}
dcl_interface fp0[2][2] = {ft0, ft1}
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 3
dp3 r0.x, v0.xyz, v0.xyz
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, v0.xyz
fcall fp0[0][0]
fcall fp0[1][1]
add r0.xyz, r0.xyz, r1.xyz
mul r0.xyz, r0.xyz, cb3[1].xxx
mov r0.w, l(1)
add o0, r0, cb3[0]
ret
label fb0
mov r0.w, this[0].y
mov r1.w, this[0].x
dp3 r2.x, cb[r1.w][r0.w].xyz, cb[r1.w][r0.w].xyz
rsq r2.x, r2.x
mul r2.xyz, r2.xxx, cb[r1.w][r0.w].xyz
dp3_sat r2.x, r0.xyz, r2.xyz
mul r1.z, r2.x, cb[r1.w][r0.w].w
mov r1.xy, r1.zz
ret
label fb1
mov r0.w, this[0].y
mov r1.w, this[0].x
dp3_sat r0.x, r0.xyz, cb[r1.w][r0.w].xyz
mul r1.xyz, r0.xxx, cb[r1.w][r0.w + 1].xyz
ret
label fb2
mov r0.w, this[1].y
mov r1.w, this[1].x
dp3 r2.x, cb[r1.w][r0.w].xyz, cb[r1.w][r0.w].xyz
rsq r2.x, r2.x
mul r2.xyz, r2.xxx, cb[r1.w][r0.w].xyz
dp3_sat r2.x, v0.xyz, r2.xyz
mul r0.z, r2.x, cb[r1.w][r0.w].w
mov r0.xy, r0.zz
ret
label fb3
mov r0.w, this[1].y
mov r1.w, this[1].x
dp3_sat r2.x, v0.xyz, cb[r1.w][r0.w].xyz
mul r0.xyz, r2.xxx, cb[r1.w][r0.w + 1].xyz
ret
