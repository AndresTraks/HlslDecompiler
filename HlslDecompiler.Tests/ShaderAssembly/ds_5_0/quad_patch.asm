ds_5_0
dcl_input_control_point_count 4
dcl_tessellator_domain domain_quad
dcl_globalFlags refactoringAllowed
dcl_constantbuffer cb0[4], immediateIndexed
dcl_input_siv vpc2.x, finalQuadUEq1EdgeTessFactor
dcl_input_siv vpc4.x, finalQuadUInsideTessFactor
dcl_input vDomain.xy
dcl_input vicp[4][0].xyz
dcl_output_siv o0, position
dcl_temps 2
add r0.xyz, -vicp[3][0].xyz, vicp[2][0].xyz
mad r0.xyz, vDomain.xxx, r0.xyz, vicp[3][0].xyz
add r1.xyz, -vicp[0][0].xyz, vicp[1][0].xyz
mad r1.xyz, vDomain.xxx, r1.xyz, vicp[0][0].xyz
add r0.xyz, r0.xyz, -r1.xyz
mad r0.xyz, vDomain.yyy, r0.xyz, r1.xyz
mad r0.y, vpc4.x, vpc2.x, r0.y
mov r0.w, l(1)
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
ret
