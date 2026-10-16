ps_5_0
dcl_globalFlags refactoringAllowed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_body fb2
dcl_function_body fb3
dcl_function_body fb4
dcl_function_body fb5
dcl_function_table ft0 = {fb0, fb2, fb4}
dcl_function_table ft1 = {fb1, fb3, fb5}
dcl_interface fp0[3][3] = {ft0, ft1}
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 2
fcall fp0[0][0]
fcall fp0[1][1]
add r0.xyz, r0.xyz, r1.xyz
fcall fp0[2][2]
add o0.xyz, r0.xyz, r1.xyz
mov o0.w, l(1)
ret
label fb0
mov r0.w, this[0].y
mov r1.x, this[0].x
mov r0.xyz, cb[r1.x][r0.w].xyz
ret
label fb1
mov r0.w, this[0].y
mov r1.x, this[0].x
dp3_sat r0.z, v0.xyz, cb[r1.x][r0.w].xyz
mov r0.xy, r0.zz
ret
label fb2
mov r0.w, this[1].y
mov r1.w, this[1].x
mov r1.xyz, cb[r1.w][r0.w].xyz
ret
label fb3
mov r0.w, this[1].y
mov r1.w, this[1].x
dp3_sat r1.z, v0.xyz, cb[r1.w][r0.w].xyz
mov r1.xy, r1.zz
ret
label fb4
mov r0.w, this[2].y
mov r1.w, this[2].x
mov r1.xyz, cb[r1.w][r0.w].xyz
ret
label fb5
mov r0.w, this[2].y
mov r1.w, this[2].x
dp3_sat r1.z, v0.xyz, cb[r1.w][r0.w].xyz
mov r1.xy, r1.zz
ret
